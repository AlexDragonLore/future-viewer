import { defineStore } from 'pinia'
import { ref } from 'vue'
import { adminApi } from '@/api/adminApi'
import { extractApiError } from '@/api/httpClient'
import type { FeedbackStatus } from '@/types'
import type {
  AdminFeedback,
  AdminUserDetail,
  AdminUserListItem,
  CreateAdminFeedbackPayload,
  CreateSyntheticFeedbackPayload,
  SetSubscriptionPayload,
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

  const users = ref<AdminUserListItem[]>([])
  const userTotal = ref(0)
  const userPage = ref(1)
  const userPageSize = ref(20)
  const userSearch = ref<string | null>(null)
  const userLoading = ref(false)
  const userError = ref<string | null>(null)
  const userToast = ref<string | null>(null)

  const selectedUser = ref<AdminUserDetail | null>(null)
  const selectedUserLoading = ref(false)
  const selectedUserError = ref<string | null>(null)

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

  async function loadUsers(): Promise<void> {
    userLoading.value = true
    userError.value = null
    try {
      const result = await adminApi.listUsers({
        search: userSearch.value,
        page: userPage.value,
        pageSize: userPageSize.value,
      })
      users.value = result.items
      userTotal.value = result.total
    } catch (e) {
      userError.value = extractApiError(e, 'Не удалось загрузить пользователей')
    } finally {
      userLoading.value = false
    }
  }

  function setUserPage(page: number): void {
    userPage.value = Math.max(1, page)
  }

  function setUserSearch(value: string | null): void {
    userSearch.value = value && value.trim() !== '' ? value.trim() : null
    userPage.value = 1
  }

  async function loadUserDetail(id: string): Promise<void> {
    selectedUserLoading.value = true
    selectedUserError.value = null
    try {
      selectedUser.value = await adminApi.getUser(id)
    } catch (e) {
      selectedUserError.value = extractApiError(e, 'Не удалось загрузить пользователя')
      selectedUser.value = null
    } finally {
      selectedUserLoading.value = false
    }
  }

  function clearUserDetail(): void {
    selectedUser.value = null
    selectedUserError.value = null
  }

  async function setUserAdmin(id: string, isAdmin: boolean): Promise<boolean> {
    userError.value = null
    try {
      const updated = await adminApi.setUserAdmin(id, isAdmin)
      const idx = users.value.findIndex((u) => u.id === id)
      if (idx >= 0) users.value[idx] = updated
      if (selectedUser.value?.id === id) {
        selectedUser.value = { ...selectedUser.value, isAdmin: updated.isAdmin }
      }
      userToast.value = updated.isAdmin ? 'Пользователь назначен админом' : 'Права админа сняты'
      return true
    } catch (e) {
      userError.value = extractApiError(e, 'Не удалось изменить роль')
      return false
    }
  }

  async function deleteUser(id: string): Promise<boolean> {
    userError.value = null
    try {
      await adminApi.deleteUser(id)
      users.value = users.value.filter((u) => u.id !== id)
      userTotal.value = Math.max(0, userTotal.value - 1)
      if (selectedUser.value?.id === id) selectedUser.value = null
      userToast.value = 'Пользователь удалён'
      return true
    } catch (e) {
      userError.value = extractApiError(e, 'Не удалось удалить пользователя')
      return false
    }
  }

  async function setUserSubscription(id: string, payload: SetSubscriptionPayload): Promise<boolean> {
    userError.value = null
    try {
      const detail = await adminApi.setUserSubscription(id, payload)
      selectedUser.value = detail
      const idx = users.value.findIndex((u) => u.id === id)
      if (idx >= 0) {
        users.value[idx] = {
          ...users.value[idx],
          subscriptionStatus: detail.subscriptionStatus,
          subscriptionExpiresAt: detail.subscriptionExpiresAt,
        }
      }
      userToast.value = 'Подписка обновлена'
      return true
    } catch (e) {
      userError.value = extractApiError(e, 'Не удалось обновить подписку')
      return false
    }
  }

  function clearUserToast(): void {
    userToast.value = null
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
    users,
    userTotal,
    userPage,
    userPageSize,
    userSearch,
    userLoading,
    userError,
    userToast,
    selectedUser,
    selectedUserLoading,
    selectedUserError,
    loadUsers,
    setUserPage,
    setUserSearch,
    loadUserDetail,
    clearUserDetail,
    setUserAdmin,
    deleteUser,
    setUserSubscription,
    clearUserToast,
  }
})
