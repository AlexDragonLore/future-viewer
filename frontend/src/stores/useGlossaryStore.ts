import { defineStore } from 'pinia'
import { ref } from 'vue'
import { glossaryApi } from '@/api/glossaryApi'
import { extractApiError } from '@/api/httpClient'
import type { CardGlossary } from '@/types'

export const useGlossaryStore = defineStore('glossary', () => {
  const cards = ref<CardGlossary[]>([])
  const detail = ref<CardGlossary | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function loadAll() {
    error.value = null
    if (cards.value.length > 0) return
    loading.value = true
    try {
      cards.value = await glossaryApi.list()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить глоссарий')
    } finally {
      loading.value = false
    }
  }

  async function loadDetail(id: number) {
    loading.value = true
    error.value = null
    detail.value = null
    try {
      detail.value = await glossaryApi.get(id)
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить карту')
    } finally {
      loading.value = false
    }
  }

  return { cards, detail, loading, error, loadAll, loadDetail }
})
