import { defineStore } from 'pinia'
import { ref } from 'vue'
import { leaderboardApi } from '@/api/leaderboardApi'
import { telegramApi } from '@/api/telegramApi'
import { feedbackApi } from '@/api/feedbackApi'
import { extractApiError } from '@/api/httpClient'
import type { FeedbackInfo, TelegramStatus, UserScoreSummary } from '@/types'

export const useProfileStore = defineStore('profile', () => {
  const summary = ref<UserScoreSummary | null>(null)
  const telegram = ref<TelegramStatus | null>(null)
  const feedbacks = ref<FeedbackInfo[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function loadSummary() {
    try {
      summary.value = await leaderboardApi.me()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить рейтинг')
    }
  }

  async function loadTelegram() {
    try {
      telegram.value = await telegramApi.status()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить статус Telegram')
    }
  }

  async function loadFeedbacks() {
    try {
      feedbacks.value = await feedbackApi.getMy()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить историю откликов')
    }
  }

  async function loadAll() {
    loading.value = true
    error.value = null
    try {
      await Promise.all([loadSummary(), loadTelegram(), loadFeedbacks()])
    } finally {
      loading.value = false
    }
  }

  return {
    summary,
    telegram,
    feedbacks,
    loading,
    error,
    loadSummary,
    loadTelegram,
    loadFeedbacks,
    loadAll,
  }
})
