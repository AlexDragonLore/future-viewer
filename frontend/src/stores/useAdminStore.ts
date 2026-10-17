import { resetOnAccountChange } from '@/utils/accountSession'
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { createAdminPageCache } from '@/utils/adminPageCache'
import { adminApi } from '@/api/adminApi'
import { extractApiError } from '@/api/httpClient'
import type { FeedbackStatus } from '@/types'
import type {
  AdminFeedback,
  AdminReading,
  AdminReadingAudience,
  AdminReadingListResponse,
  AdminUserListResponse,
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
  const readingAudience = ref<AdminReadingAudience>('all')
  const readingLoading = ref(false)
  const readingError = ref<string | null>(null)
  const readingDisplayedPage = ref(1)
  const userDisplayedPage = ref(1)
  const readingCache = createAdminPageCache<AdminReadingListResponse>()
  const userCache = createAdminPageCache<AdminUserListResponse>()
  let readingController: AbortController | undefined
  let userController: AbortController | undefined
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

  resetOnAccountChange({ readingDisplayedPage, userDisplayedPage, readings, readingTotal, readingPage, readingPageSize, readingSearch, readingAudience, readingLoading, readingError, feedbacks, feedbackTotal, feedbackPage, feedbackPageSize, feedbackUserFilter, feedbackStatusFilter, feedbackLoading, feedbackError, feedbackToast, users, userTotal, userPage, userPageSize, userSearch, userLoading, userError, userToast, selectedUser, selectedUserLoading, selectedUserError, stats, statsLoading, statsError }, () => {
    readingRequest++; userRequest++; detailRequest++; feedbackRequest++
    readingController?.abort(); userController?.abort()
    readingCache.clear(); userCache.clear()
  })

  async function loadReadings(force = false): Promise<void> {
    const request = ++readingRequest
    readingController?.abort()
    const controller = new AbortController()
    readingController = controller
    const page = readingPage.value
    const filters = { search: readingSearch.value, audience: readingAudience.value, page, pageSize: readingPageSize.value, includeInterpretation: false }
    const key = JSON.stringify(filters)
    if (force) readingCache.clear()
    readingLoading.value = true
    readingError.value = null
    try {
      const cached = readingCache.get(key)
      const result = cached ?? await adminApi.listReadings(filters, controller.signal)
      if (request !== readingRequest) return
      if (!cached) readingCache.set(key, result)
      readings.value = result.items
      readingTotal.value = result.total
      readingDisplayedPage.value = page
    } catch (e) {
      if (request !== readingRequest) return
      readingPage.value = readingDisplayedPage.value
      readingError.value = extractApiError(e, 'Не удалось загрузить сообщения')
    } finally {
      if (request === readingRequest) readingLoading.value = false
    }
  }

  function clearReadingResults(): void {
    readingRequest++
    readingController?.abort()
    readingCache.clear()
    readings.value = []
    readingTotal.value = 0
    readingPage.value = 1
    readingDisplayedPage.value = 1
    readingLoading.value = false
    readingError.value = null
  }

  function setReadingSearch(value: string): void {
    clearReadingResults()
    readingSearch.value = value.trim() || null
  }

  function setReadingAudience(value: AdminReadingAudience): void {
    if (readingAudience.value === value) return
    clearReadingResults()
    readingAudience.value = value
  }

  function setReadingPage(value: number): void {
    readingPage.value = Math.max(1, value)
  }

  function invalidateAdminPages(): void {
    readingCache.clear()
    userCache.clear()
    readingRequest++; userRequest++
    readingController?.abort(); userController?.abort()
    readingLoading.value = false; userLoading.value = false
    readingPage.value = readingDisplayedPage.value
    userPage.value = userDisplayedPage.value
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
      invalidateAdminPages()
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
      invalidateAdminPages()
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
      invalidateAdminPages()
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
      invalidateAdminPages()
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

  async function loadUsers(force = false): Promise<void> {
    const request = ++userRequest
    userController?.abort()
    const controller = new AbortController()
    userController = controller
    const page = userPage.value
    const filters = { search: userSearch.value, page, pageSize: userPageSize.value }
    const key = JSON.stringify(filters)
    if (force) userCache.clear()
    userLoading.value = true
    userError.value = null
    try {
      const cached = userCache.get(key)
      const result = cached ?? await adminApi.listUsers(filters, controller.signal)
      if (request !== userRequest) return
      if (!cached) userCache.set(key, result)
      users.value = result.items
      userTotal.value = result.total
      userDisplayedPage.value = page
    } catch (e) {
      if (request !== userRequest) return
      userPage.value = userDisplayedPage.value
      userError.value = extractApiError(e, 'Не удалось загрузить пользователей')
    } finally {
      if (request === userRequest) userLoading.value = false
    }
  }

  function setUserPage(page: number): void {
    userPage.value = Math.max(1, page)
  }

  function setUserSearch(value: string | null): void {
    userRequest++
    userController?.abort()
    userCache.clear()
    users.value = []
    userTotal.value = 0
    userSearch.value = value && value.trim() !== '' ? value.trim() : null
    userPage.value = 1
    userDisplayedPage.value = 1
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
      invalidateAdminPages()
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
      invalidateAdminPages()
      readings.value = readings.value.filter(reading => reading.userId !== id)
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
      invalidateAdminPages()
      if (readingAudience.value !== 'subscribers') {
        readings.value = readings.value.map(reading => reading.userId === id ? { ...reading, subscription: detail.subscription } : reading)
      }
      if (selectedUser.value?.id === id) selectedUser.value = detail
      const idx = users.value.findIndex((u) => u.id === id)
      if (idx >= 0) {
        users.value[idx] = {
          ...users.value[idx],
          subscriptionStatus: detail.subscriptionStatus,
          subscriptionExpiresAt: detail.subscriptionExpiresAt,
          subscription: detail.subscription,
        }
      }
      if (readingAudience.value === 'subscribers') {
        // Access edits can add/remove every question by this user from the filtered result.
        // Reload from page one so the total and membership stay correct, including on failure.
        clearReadingResults()
        await loadReadings()
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
      invalidateAdminPages()
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
      invalidateAdminPages()
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
      invalidateAdminPages()
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
    readingDisplayedPage, userDisplayedPage,
    readings, readingTotal, readingPage, readingPageSize, readingSearch, readingAudience, readingLoading, readingError,
    loadReadings, setReadingSearch, setReadingAudience, setReadingPage,
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
