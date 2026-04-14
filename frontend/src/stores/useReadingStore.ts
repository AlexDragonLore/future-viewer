import { defineStore } from 'pinia'
import { ref } from 'vue'
import { readingApi } from '@/api/readingApi'
import type { Reading, SpreadInfo, SpreadType } from '@/types'

export const useReadingStore = defineStore('reading', () => {
  const spreads = ref<SpreadInfo[]>([])
  const current = ref<Reading | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function loadSpreads() {
    if (spreads.value.length > 0) return
    spreads.value = await readingApi.spreads()
  }

  async function create(spreadType: SpreadType, question: string) {
    loading.value = true
    error.value = null
    try {
      current.value = await readingApi.create(spreadType, question)
    } catch (e: any) {
      error.value = e.response?.data?.message ?? e.message ?? 'Unknown error'
      throw e
    } finally {
      loading.value = false
    }
  }

  function reset() {
    current.value = null
    error.value = null
  }

  return { spreads, current, loading, error, loadSpreads, create, reset }
})
