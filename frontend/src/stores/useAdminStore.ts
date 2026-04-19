import { defineStore } from 'pinia'
import { ref } from 'vue'
import { adminApi } from '@/api/adminApi'
import { extractApiError } from '@/api/httpClient'
import type { FeedbackStatus } from '@/types'
import type {
  AdminFeedback,
  CreateAdminFeedbackPayload,
  CreateSyntheticFeedbackPayload,
  UpdateAdminFeedbackPayload,
} from '@/types/admin'

export const useAdminStore = defineStore('admin', () => {
  const feedbacks = ref<AdminFeedback[]>([])
  const feedbackTotal = ref(0)
  const feedbackPage = ref(1)
  const feedbackPageSize = ref(20)
  const feedbackUserFilter = ref<string | null>(null)
  const feedbackStatusFilter = ref<FeedbackStatus | null>(null)
  const feedbackLoading = ref(false)
  const feedbackError = ref<string | null>(null)
  const feedbackToast = ref<string | null>(null)

  async function loadFeedbacks(): Promise<void> {
    feedbackLoading.value = true
    feedbackError.value = null
    try {
      const result = await adminApi.listFeedbacks({
        userId: feedbackUserFilter.value,
        status: feedbackStatusFilter.value,
        page: feedbackPage.value,
        pageSize: feedbackPageSize.value,
      })
      feedbacks.value = result.items
      feedbackTotal.value = result.total
    } catch (e) {
      feedbackError.value = extractApiError(e, 'Не удалось загрузить фидбеки')
    } finally {
      feedbackLoading.value = false
    }
  }

  function setFeedbackPage(page: number): void {
    feedbackPage.value = Math.max(1, page)
  }

  function setFeedbackUserFilter(value: string | null): void {
    feedbackUserFilter.value = value && value.trim() !== '' ? value.trim() : null
    feedbackPage.value = 1
  }

  function setFeedbackStatusFilter(value: FeedbackStatus | null): void {
    feedbackStatusFilter.value = value
    feedbackPage.value = 1
  }

  async function createFeedback(payload: CreateAdminFeedbackPayload): Promise<AdminFeedback | null> {
    feedbackError.value = null
    try {
      const created = await adminApi.createFeedback(payload)
      feedbackToast.value = 'Фидбек создан'
      await loadFeedbacks()
      return created
    } catch (e) {
      feedbackError.value = extractApiError(e, 'Не удалось создать фидбек')
      return null
    }
  }

  async function createSyntheticFeedback(payload: CreateSyntheticFeedbackPayload): Promise<AdminFeedback | null> {
    feedbackError.value = null
    try {
      const created = await adminApi.createSyntheticFeedback(payload)
      feedbackToast.value = 'Синтетический фидбек создан'
      await loadFeedbacks()
      return created
    } catch (e) {
      feedbackError.value = extractApiError(e, 'Не удалось создать синтетический фидбек')
      return null
    }
  }

  async function updateFeedback(id: string, payload: UpdateAdminFeedbackPayload): Promise<AdminFeedback | null> {
    feedbackError.value = null
    try {
      const updated = await adminApi.updateFeedback(id, payload)
      const idx = feedbacks.value.findIndex((f) => f.id === id)
      if (idx >= 0) feedbacks.value[idx] = updated
      feedbackToast.value = 'Сохранено'
      return updated
    } catch (e) {
      feedbackError.value = extractApiError(e, 'Не удалось сохранить фидбек')
      return null
    }
  }

  async function deleteFeedback(id: string): Promise<boolean> {
    feedbackError.value = null
    try {
      await adminApi.deleteFeedback(id)
      feedbacks.value = feedbacks.value.filter((f) => f.id !== id)
      feedbackTotal.value = Math.max(0, feedbackTotal.value - 1)
      feedbackToast.value = 'Удалено'
      return true
    } catch (e) {
      feedbackError.value = extractApiError(e, 'Не удалось удалить фидбек')
      return false
    }
  }

  async function runNotifications(): Promise<number | null> {
    feedbackError.value = null
    try {
      const { processed } = await adminApi.runNotifications()
      feedbackToast.value = `Отправлено уведомлений: ${processed}`
      return processed
    } catch (e) {
      feedbackError.value = extractApiError(e, 'Не удалось запустить рассылку')
      return null
    }
  }

  function clearFeedbackToast(): void {
    feedbackToast.value = null
  }

  return {
    feedbacks,
    feedbackTotal,
    feedbackPage,
    feedbackPageSize,
    feedbackUserFilter,
    feedbackStatusFilter,
    feedbackLoading,
    feedbackError,
    feedbackToast,
    loadFeedbacks,
    setFeedbackPage,
    setFeedbackUserFilter,
    setFeedbackStatusFilter,
    createFeedback,
    createSyntheticFeedback,
    updateFeedback,
    deleteFeedback,
    runNotifications,
    clearFeedbackToast,
  }
})
