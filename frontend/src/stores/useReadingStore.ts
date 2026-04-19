import { defineStore } from 'pinia'
import { ref } from 'vue'
import { readingApi } from '@/api/readingApi'
import { extractApiError } from '@/api/httpClient'
import { useAuthStore } from '@/stores/useAuthStore'
import { useDeckStore } from '@/stores/useDeckStore'
import type { Reading, SpreadInfo, SpreadType } from '@/types'

export const useReadingStore = defineStore('reading', () => {
  const spreads = ref<SpreadInfo[]>([])
  const current = ref<Reading | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const streamingText = ref('')
  const streamingDone = ref(false)
  const cardsReady = ref(false)

  async function loadSpreads() {
    if (spreads.value.length > 0) return
    try {
      spreads.value = await readingApi.spreads()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить расклады')
    }
  }

  async function create(spreadType: SpreadType, question: string) {
    loading.value = true
    error.value = null
    try {
      current.value = await readingApi.create(spreadType, question, useDeckStore().current)
      void useAuthStore().refreshSubscription()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось создать расклад')
      throw e
    } finally {
      loading.value = false
    }
  }

  function createStream(spreadType: SpreadType, question: string) {
    loading.value = true
    error.value = null
    current.value = null
    streamingText.value = ''
    streamingDone.value = false
    cardsReady.value = false

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
          current.value = reading
          cardsReady.value = true
          resolveCards(reading)
        },
        onChunk: (delta) => {
          streamingText.value += delta
        },
        onDone: () => {
          streamingDone.value = true
          if (current.value) {
            current.value = { ...current.value, interpretation: streamingText.value }
          }
          void useAuthStore().refreshSubscription()
        },
      })
      .catch((e) => {
        const msg = extractApiError(e, 'Не удалось создать расклад')
        error.value = msg
        if (!cardsReady.value) rejectCards(e)
        throw e
      })
      .finally(() => {
        loading.value = false
      })

    return { cardsPromise, donePromise }
  }

  function reset() {
    current.value = null
    error.value = null
    streamingText.value = ''
    streamingDone.value = false
    cardsReady.value = false
  }

  return {
    spreads,
    current,
    loading,
    error,
    streamingText,
    streamingDone,
    cardsReady,
    loadSpreads,
    create,
    createStream,
    reset,
  }
})
