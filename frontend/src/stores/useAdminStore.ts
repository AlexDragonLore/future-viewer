import { resetOnAccountChange } from '@/utils/accountSession'
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { adminApi } from '@/api/adminApi'
import { extractApiError } from '@/api/httpClient'
import type { FeedbackStatus } from '@/types'
import type {
  AdminFeedback,
  AdminReading,
  AdminStats,
  AdminUserDetail,
  AdminUserListItem,
  CreateAdminFeedbackPayload,
  CreateSyntheticFeedbackPayload,
  SetSubscriptionPayload,
  UpdateAdminFeedbackPayload,
} from '@/types/admin'

export const useAdminStore = defineStore('admin', () => {
  const readings = ref<AdminReading[]>([])
  const readingTotal = ref(0)
  const readingPage = ref(1)
  const readingPageSize = ref(20)
  const readingSearch = ref<string | null>(null)
  const readingLoading = ref(false)
  const readingError = ref<string | null>(null)
  let readingRequest = 0
  let userRequest = 0
  let detailRequest = 0
  let feedbackRequest = 0

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

  const stats = ref<AdminStats | null>(null)
  const statsLoading = ref(false)
  const statsError = ref<string | null>(null)

  resetOnAccountChange({ readings, readingTotal, readingPage, readingPageSize, readingSearch, readingLoading, readingError, feedbacks, feedbackTotal, feedbackPage, feedbackPageSize, feedbackUserFilter, feedbackStatusFilter, feedbackLoading, feedbackError, feedbackToast, users, userTotal, userPage, userPageSize, userSearch, userLoading, userError, userToast, selectedUser, selectedUserLoading, selectedUserError, stats, statsLoading, statsError }, () => { readingRequest++; userRequest++; detailRequest++; feedbackRequest++ })

  async function loadReadings(): Promise<void> {
    const request = ++readingRequest
    readingLoading.value = true
    readingError.value = null
    try {
      const result = await adminApi.listReadings({
        search: readingSearch.value, page: readingPage.value, pageSize: readingPageSize.value,
      })
      if (request !== readingRequest) return
      readings.value = result.items
      readingTotal.value = result.total
    } catch (e) {
      if (request !== readingRequest) return
      readings.value = []
      readingTotal.value = 0
      readingError.value = extractApiError(e, 'Не удалось загрузить сообщения')
    } finally {
      if (request === readingRequest) readingLoading.value = false
    }
  }

  function setReadingSearch(value: string): void {
    readingSearch.value = value.trim() || null
    readingPage.value = 1
  }

  function setReadingPage(value: number): void {
    readingPage.value = Math.max(1, value)
  }

  async function loadFeedbacks(): Promise<void> {
    const request = ++feedbackRequest
    feedbackLoading.value = true
    feedbackError.value = null
    try {
      const result = await adminApi.listFeedbacks({
        userId: feedbackUserFilter.value,
        status: feedbackStatusFilter.value,
        page: feedbackPage.value,
        pageSize: feedbackPageSize.value,
      })
      if (request !== feedbackRequest) return
      feedbacks.value = result.items
      feedbackTotal.value = result.total
    } catch (e) {
      if (request !== feedbackRequest) return
      feedbackError.value = extractApiError(e, 'Не удалось загрузить отзывы')
    } finally {
      if (request === feedbackRequest) feedbackLoading.value = false
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
      feedbackToast.value = 'Отзыв создан'
      await loadFeedbacks()
      return created
    } catch (e) {
      feedbackError.value = extractApiError(e, 'Не удалось создать отзыв')
      return null
    }
  }

  async function createSyntheticFeedback(payload: CreateSyntheticFeedbackPayload): Promise<AdminFeedback | null> {
    feedbackError.value = null
    try {
      const created = await adminApi.createSyntheticFeedback(payload)
      feedbackToast.value = 'Синтетический отзыв создан'
      await loadFeedbacks()
      return created
    } catch (e) {
      feedbackError.value = extractApiError(e, 'Не удалось создать синтетический отзыв')
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
      feedbackError.value = extractApiError(e, 'Не удалось сохранить отзыв')
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
      feedbackError.value = extractApiError(e, 'Не удалось удалить отзыв')
      return false
    }
  }

  function clearFeedbackToast(): void {
    feedbackToast.value = null
  }

  async function loadUsers(): Promise<void> {
    const request = ++userRequest
    userLoading.value = true
    userError.value = null
    try {
      const result = await adminApi.listUsers({
        search: userSearch.value,
        page: userPage.value,
        pageSize: userPageSize.value,
      })
      if (request !== userRequest) return
      users.value = result.items
      userTotal.value = result.total
    } catch (e) {
      if (request !== userRequest) return
      userError.value = extractApiError(e, 'Не удалось загрузить пользователей')
    } finally {
      if (request === userRequest) userLoading.value = false
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
    const request = ++detailRequest
    selectedUser.value = null
    selectedUserLoading.value = true
    selectedUserError.value = null
    try {
      const result = await adminApi.getUser(id)
      if (request !== detailRequest) return
      selectedUser.value = result
    } catch (e) {
      if (request !== detailRequest) return
      selectedUserError.value = extractApiError(e, 'Не удалось загрузить пользователя')
      selectedUser.value = null
    } finally {
      if (request === detailRequest) selectedUserLoading.value = false
    }
  }

  function clearUserDetail(): void {
    detailRequest++
    selectedUserLoading.value = false
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
      if (selectedUser.value?.id === id) selectedUser.value = detail
      const idx = users.value.findIndex((u) => u.id === id)
      if (idx >= 0) {
        users.value[idx] = {
          ...users.value[idx],
          subscriptionStatus: detail.subscriptionStatus,
          subscriptionExpiresAt: detail.subscriptionExpiresAt,
        }
      }
      userToast.value = 'Доступ обновлён'
      return true
    } catch (e) {
      userError.value = extractApiError(e, 'Не удалось обновить доступ')
      return false
    }
  }

  async function grantAchievement(id: string, code: string): Promise<boolean> {
    userError.value = null
    try {
      await adminApi.grantAchievement(id, code)
      userToast.value = `Достижение ${code} выдана`
      if (selectedUser.value?.id === id) await loadUserDetail(id)
      return true
    } catch (e) {
      userError.value = extractApiError(e, 'Не удалось выдать достижение')
      return false
    }
  }

  async function revokeAchievement(id: string, code: string): Promise<boolean> {
    userError.value = null
    try {
      await adminApi.revokeAchievement(id, code)
      userToast.value = `Достижение ${code} снята`
      if (selectedUser.value?.id === id) await loadUserDetail(id)
      return true
    } catch (e) {
      userError.value = extractApiError(e, 'Не удалось снять достижение')
      return false
    }
  }

  async function recheckAchievements(id: string): Promise<number | null> {
    userError.value = null
    try {
      const granted = await adminApi.recheckAchievements(id)
      userToast.value = granted.length > 0
        ? `Выдано новых достижений: ${granted.length}`
        : 'Новых достижений нет'
      if (selectedUser.value?.id === id) await loadUserDetail(id)
      return granted.length
    } catch (e) {
      userError.value = extractApiError(e, 'Не удалось пересчитать достижения')
      return null
    }
  }

  function clearUserToast(): void {
    userToast.value = null
  }

  async function loadStats(): Promise<void> {
    statsLoading.value = true
    statsError.value = null
    try {
      stats.value = await adminApi.getStats()
    } catch (e) {
      statsError.value = extractApiError(e, 'Не удалось загрузить статистику')
    } finally {
      statsLoading.value = false
    }
  }

  return {
    readings, readingTotal, readingPage, readingPageSize, readingSearch, readingLoading, readingError,
    loadReadings, setReadingSearch, setReadingPage,
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
    grantAchievement,
    revokeAchievement,
    recheckAchievements,
    clearUserToast,
    stats,
    statsLoading,
    statsError,
    loadStats,
  }
})
