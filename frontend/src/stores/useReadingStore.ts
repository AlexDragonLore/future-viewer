import { accountSessionVersion, resetOnAccountChange } from '@/utils/accountSession'
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { readingApi } from '@/api/readingApi'
import { extractApiError } from '@/api/httpClient'
import { useAuthStore } from '@/stores/useAuthStore'
import { useDeckStore } from '@/stores/useDeckStore'
import type { Reading, SpreadInfo, SpreadType } from '@/types'
import { clearGuestContinuation, clearUnlockedGuestReading, getGuestContinuation, getUnlockedGuestReading, saveGuestContinuation, saveUnlockedGuestReading } from '@/utils/guestReading'
import { trackGoal, trackGoalOnce } from '@/analytics/metrika'

export interface PendingReading {
  spreadType: SpreadType
  question: string
  questionWarningAcknowledged: boolean
  validated: boolean
}

export interface ReadingWorkflowIssue {
  message: string
  suggestedQuestion?: string | null
}

export const useReadingStore = defineStore('reading', () => {
  const spreads = ref<SpreadInfo[]>([])
  const current = ref<Reading | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const pending = ref<PendingReading | null>(null)
  const workflowIssue = ref<ReadingWorkflowIssue | null>(null)
  const guestUnlockBlocked = ref(false)

  const streamingText = ref('')
  const streamingDone = ref(false)
  const cardsReady = ref(false)
  let streamBuffer = ''
  let streamFlushRaf: number | null = null

  resetOnAccountChange({ current, loading, error, pending, workflowIssue, guestUnlockBlocked, streamingText, streamingDone, cardsReady }, cancelStreamFlush)

  function flushStreamBuffer() {
    streamFlushRaf = null
    if (!streamBuffer) return
    streamingText.value += streamBuffer
    streamBuffer = ''
  }

  function appendStreamingChunk(delta: string) {
    streamBuffer += delta
    if (streamFlushRaf !== null) return

    if (typeof requestAnimationFrame === 'undefined') {
      flushStreamBuffer()
      return
    }

    streamFlushRaf = requestAnimationFrame(flushStreamBuffer)
  }

  function cancelStreamFlush() {
    if (streamFlushRaf !== null && typeof cancelAnimationFrame !== 'undefined') {
      cancelAnimationFrame(streamFlushRaf)
    }
    streamFlushRaf = null
    streamBuffer = ''
  }

  function isAbortError(e: unknown) {
    return e instanceof Error && e.name === 'AbortError'
  }

  async function loadSpreads() {
    if (spreads.value.length > 0) return
    try {
      spreads.value = await readingApi.spreads()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить расклады')
    }
  }

  function createGuest(question: string, signal?: AbortSignal) {
    reset()
    loading.value = true
    const session = accountSessionVersion()
    trackGoal('guest_reading_started')
    const cardsPromise = readingApi.createGuest(question, useDeckStore().current, signal)
      .then((response) => {
        if (session !== accountSessionVersion() || signal?.aborted) throw new DOMException('Сеанс изменён', 'AbortError')
        saveGuestContinuation({ ticket: response.ticket, expiresAt: response.expiresAt })
        current.value = response.reading
        cardsReady.value = true
        streamingText.value = response.reading.interpretation ?? ''
        streamingDone.value = true
        return response.reading
      })
      .catch((e) => {
        if (session === accountSessionVersion() && !signal?.aborted) {
          error.value = extractApiError(e, 'Не удалось открыть расклад. Попробуйте ещё раз.')
        }
        throw e
      })
      .finally(() => {
        if (session === accountSessionVersion()) loading.value = false
      })
    return { cardsPromise, donePromise: cardsPromise.then(() => undefined) }
  }

  async function restoreGuest() {
    const continuation = getGuestContinuation()
    const auth = useAuthStore()
    const authenticated = auth.isAuthenticated
    const unlocked = authenticated ? getUnlockedGuestReading(auth.userId) : null
    if (!continuation && !unlocked) return false
    const session = accountSessionVersion()
    loading.value = true
    error.value = null
    guestUnlockBlocked.value = false
    const applyReading = (result: Reading) => {
      cancelStreamFlush()
      current.value = result
      streamingText.value = result.interpretation ?? ''
      streamingDone.value = true
      cardsReady.value = true
    }
    try {
      const result = continuation
        ? authenticated
          ? await readingApi.unlockGuest(continuation.ticket)
          : await readingApi.guestPreview(continuation.ticket)
        : await readingApi.get(unlocked!.readingId)
      if (session !== accountSessionVersion()) return false
      applyReading(result)
      if (authenticated && continuation) {
        trackGoalOnce('guest_reading_unlocked', result.id)
        if (auth.userId) {
          saveUnlockedGuestReading({ readingId: result.id, ownerUserId: auth.userId, expiresAt: continuation.expiresAt })
        }
        clearGuestContinuation()
        void useAuthStore().refreshSubscription()
      }
      return true
    } catch (e) {
      if (session === accountSessionVersion()) {
        error.value = extractApiError(e, 'Не удалось восстановить расклад. Попробуйте ещё раз.')
        const response = (e as { response?: { status?: number; data?: { error?: string } } }).response
        const status = response?.status
        if (status === 404) {
          if (continuation) clearGuestContinuation()
          else clearUnlockedGuestReading()
        }
        if (authenticated && continuation && (status === 402 || status === 429)) {
          guestUnlockBlocked.value = status === 402 || response?.data?.error === 'quota_exceeded'
          try {
            const preview = current.value?.isPreview ? current.value : await readingApi.guestPreview(continuation.ticket)
            if (session === accountSessionVersion()) applyReading(preview)
          } catch {
            // Keep the unlock error and ticket so the preview can be restored later.
          }
          if (session === accountSessionVersion()) void useAuthStore().refreshSubscription()
        }
      }
      return false
    } finally {
      if (session === accountSessionVersion()) loading.value = false
    }
  }

  async function create(spreadType: SpreadType, question: string, questionWarningAcknowledged = false) {
    clearUnlockedGuestReading()
    loading.value = true
    error.value = null
    try {
      current.value = await readingApi.create(
        spreadType,
        question,
        useDeckStore().current,
        questionWarningAcknowledged,
      )
      void useAuthStore().refreshSubscription()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось создать расклад')
      throw e
    } finally {
      loading.value = false
    }
  }

  function createStream(
    spreadType: SpreadType,
    question: string,
    signal?: AbortSignal,
    questionWarningAcknowledged = false,
  ) {
    clearUnlockedGuestReading()
    loading.value = true
    error.value = null
    current.value = null
    cancelStreamFlush()
    streamingText.value = ''
    streamingDone.value = false
    cardsReady.value = false

    const session = accountSessionVersion()
    const deckType = useDeckStore().current

    let resolveCards!: (reading: Reading) => void
    let rejectCards!: (e: unknown) => void
    const cardsPromise = new Promise<Reading>((res, rej) => {
      resolveCards = res
      rejectCards = rej
    })

    const donePromise = readingApi
      .createStream(spreadType, question, deckType, {
        onCards: (reading) => {
          if (session !== accountSessionVersion()) return
          current.value = reading
          cardsReady.value = true
          resolveCards(reading)
        },
        onChunk: (delta) => {
          if (session !== accountSessionVersion()) return
          appendStreamingChunk(delta)
        },
        onDone: () => {
          if (session !== accountSessionVersion()) return
          flushStreamBuffer()
          streamingDone.value = true
          if (current.value) {
            current.value = { ...current.value, interpretation: streamingText.value }
          }
          void useAuthStore().refreshSubscription()
        },
      }, signal, questionWarningAcknowledged)
      .catch((e) => {
        if (session !== accountSessionVersion()) {
          rejectCards(e)
          throw e
        }
        flushStreamBuffer()
        if (isAbortError(e)) {
          if (!cardsReady.value) rejectCards(e)
          throw e
        }

        const msg = extractApiError(e, 'Не удалось создать расклад')
        error.value = msg
        const text = streamingText.value ? `${streamingText.value}\n\n> ${msg}` : msg
        streamingText.value = text
        streamingDone.value = true
        if (current.value) {
          current.value = { ...current.value, interpretation: text }
        }
        if (!cardsReady.value) rejectCards(e)
        throw e
      })
      .finally(() => {
        if (session === accountSessionVersion()) loading.value = false
      })

    return { cardsPromise, donePromise }
  }

  function reset() {
    current.value = null
    error.value = null
    guestUnlockBlocked.value = false
    cancelStreamFlush()
    streamingText.value = ''
    streamingDone.value = false
    cardsReady.value = false
  }

  function setPending(value: PendingReading) {
    pending.value = value
  }

  function takePending() {
    const value = pending.value
    pending.value = null
    return value
  }

  function setWorkflowIssue(value: ReadingWorkflowIssue) {
    workflowIssue.value = value
  }

  function takeWorkflowIssue() {
    const value = workflowIssue.value
    workflowIssue.value = null
    return value
  }

  return {
    spreads,
    current,
    loading,
    error,
    pending,
    workflowIssue,
    guestUnlockBlocked,
    streamingText,
    streamingDone,
    cardsReady,
    loadSpreads,
    create,
    createStream,
    createGuest,
    restoreGuest,
    reset,
    setPending,
    takePending,
    setWorkflowIssue,
    takeWorkflowIssue,
  }
})
