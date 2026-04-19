import { defineStore } from 'pinia'
import { ref } from 'vue'
import { leaderboardApi } from '@/api/leaderboardApi'
import { extractApiError } from '@/api/httpClient'
import type { LeaderboardEntry } from '@/types'

export const useLeaderboardStore = defineStore('leaderboard', () => {
  const monthly = ref<LeaderboardEntry[]>([])
  const allTime = ref<LeaderboardEntry[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function loadMonthly(year?: number, month?: number) {
    loading.value = true
    error.value = null
    try {
      monthly.value = await leaderboardApi.monthly(year, month)
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить рейтинг')
    } finally {
      loading.value = false
    }
  }

  async function loadAllTime() {
    loading.value = true
    error.value = null
    try {
      allTime.value = await leaderboardApi.alltime()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить рейтинг')
    } finally {
      loading.value = false
    }
  }

  return { monthly, allTime, loading, error, loadMonthly, loadAllTime }
})
