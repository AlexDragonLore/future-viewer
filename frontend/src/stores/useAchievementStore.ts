import { defineStore } from 'pinia'
import { ref } from 'vue'
import { achievementApi } from '@/api/achievementApi'
import { extractApiError } from '@/api/httpClient'
import type { AchievementInfo } from '@/types'

export const useAchievementStore = defineStore('achievement', () => {
  const items = ref<AchievementInfo[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function loadCatalog() {
    loading.value = true
    error.value = null
    try {
      items.value = await achievementApi.all()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить ачивки')
    } finally {
      loading.value = false
    }
  }

  async function loadMine() {
    loading.value = true
    error.value = null
    try {
      items.value = await achievementApi.mine()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить ачивки')
    } finally {
      loading.value = false
    }
  }

  return { items, loading, error, loadCatalog, loadMine }
})
