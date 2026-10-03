import { accountSessionVersion, resetOnAccountChange } from '@/utils/accountSession'
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { readingApi } from '@/api/readingApi'
import { extractApiError } from '@/api/httpClient'
import { useAuthStore } from '@/stores/useAuthStore'
import { useDeckStore } from '@/stores/useDeckStore'
import type { Reading, SpreadInfo, SpreadType } from '@/types'
import { clearGuestContinuation, getGuestContinuation, saveGuestContinuation } from '@/utils/guestReading'
import { trackGoal, trackGoalOnce } from '@/analytics/metrika'

export interface PendingReading {
  spreadType: SpreadType
  question: string
  questionWarningAcknowledged: boolean
  saveToHistory: boolean
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

  const streamingText = ref('')
  const streamingDone = ref(false)
  const cardsReady = ref(false)
  let streamBuffer = ''
  let streamFlushRaf: number | null = null

  resetOnAccountChange({ current, loading, error, pending, workflowIssue, streamingText, streamingDone, cardsReady }, cancelStreamFlush)

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
          error.value = extractApiError(e, 'Не удалось открыть карту. Попробуйте ещё раз.')
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
    if (!continuation) return false
    const session = accountSessionVersion()
    loading.value = true
    error.value = null
    const authenticated = useAuthStore().isAuthenticated
    try {
      const result = authenticated
        ? await readingApi.unlockGuest(continuation.ticket)
        : await readingApi.guestPreview(continuation.ticket)
      if (session !== accountSessionVersion()) return false
      cancelStreamFlush()
      current.value = result
      streamingText.value = result.interpretation ?? ''
      streamingDone.value = true
      cardsReady.value = true
      if (authenticated) {
        trackGoalOnce('guest_reading_unlocked', result.id)
        clearGuestContinuation()
        void useAuthStore().refreshSubscription()
      }
      return true
    } catch (e) {
      if (session === accountSessionVersion()) {
        error.value = extractApiError(e, 'Не удалось восстановить расклад. Попробуйте ещё раз.')
        if ((e as { response?: { status?: number } }).response?.status === 404) clearGuestContinuation()
      }
      return false
    } finally {
      if (session === accountSessionVersion()) loading.value = false
    }
  }

  async function create(spreadType: SpreadType, question: string, questionWarningAcknowledged = false, saveToHistory = false) {
    loading.value = true
    error.value = null
    try {
      current.value = await readingApi.create(
        spreadType,
        question,
        useDeckStore().current,
        questionWarningAcknowledged,
        saveToHistory,
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
    saveToHistory = false,
  ) {
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
      }, signal, questionWarningAcknowledged, saveToHistory)
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
