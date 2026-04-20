import { httpClient } from './httpClient'
import type {
  AdminFeedback,
  AdminFeedbackListResponse,
  AdminGrantedAchievement,
  AdminStats,
  AdminTelegramLinkResult,
  AdminUserDetail,
  AdminUserListItem,
  AdminUserListResponse,
  CreateAdminFeedbackPayload,
  CreateSyntheticFeedbackPayload,
  FeedbackSearchFilters,
  RunNotificationsResult,
  SetSubscriptionPayload,
  UpdateAdminFeedbackPayload,
  UserSearchFilters,
} from '@/types/admin'

export const adminApi = {
  async listFeedbacks(filters: FeedbackSearchFilters = {}): Promise<AdminFeedbackListResponse> {
    const { data } = await httpClient.get<AdminFeedbackListResponse>('/api/admin/feedbacks', {
      params: {
        userId: filters.userId || undefined,
        status: filters.status ?? undefined,
        page: filters.page ?? 1,
        pageSize: filters.pageSize ?? 20,
      },
    })
    return data
  },

  async createFeedback(payload: CreateAdminFeedbackPayload): Promise<AdminFeedback> {
    const { data } = await httpClient.post<AdminFeedback>('/api/admin/feedbacks', payload)
    return data
  },

  async createSyntheticFeedback(payload: CreateSyntheticFeedbackPayload): Promise<AdminFeedback> {
    const { data } = await httpClient.post<AdminFeedback>('/api/admin/feedbacks/synthetic', payload)
    return data
  },

  async updateFeedback(id: string, payload: UpdateAdminFeedbackPayload): Promise<AdminFeedback> {
    const { data } = await httpClient.put<AdminFeedback>(`/api/admin/feedbacks/${id}`, payload)
    return data
  },

  async deleteFeedback(id: string): Promise<void> {
    await httpClient.delete(`/api/admin/feedbacks/${id}`)
  },

  async runNotifications(): Promise<RunNotificationsResult> {
    const { data } = await httpClient.post<RunNotificationsResult>('/api/admin/feedbacks/run-notifications')
    return data
  },

  async listUsers(filters: UserSearchFilters = {}): Promise<AdminUserListResponse> {
    const { data } = await httpClient.get<AdminUserListResponse>('/api/admin/users', {
      params: {
        search: filters.search || undefined,
        page: filters.page ?? 1,
        pageSize: filters.pageSize ?? 20,
      },
    })
    return data
  },

  async getUser(id: string): Promise<AdminUserDetail> {
    const { data } = await httpClient.get<AdminUserDetail>(`/api/admin/users/${id}`)
    return data
  },

  async setUserAdmin(id: string, isAdmin: boolean): Promise<AdminUserListItem> {
    const { data } = await httpClient.put<AdminUserListItem>(`/api/admin/users/${id}/admin`, { isAdmin })
    return data
  },

  async deleteUser(id: string): Promise<void> {
    await httpClient.delete(`/api/admin/users/${id}`)
  },

  async setUserSubscription(id: string, payload: SetSubscriptionPayload): Promise<AdminUserDetail> {
    const { data } = await httpClient.put<AdminUserDetail>(`/api/admin/users/${id}/subscription`, payload)
    return data
  },

  async grantAchievement(id: string, code: string): Promise<AdminGrantedAchievement> {
    const { data } = await httpClient.post<AdminGrantedAchievement>(
      `/api/admin/users/${id}/achievements`,
      { code },
    )
    return data
  },

  async revokeAchievement(id: string, code: string): Promise<void> {
    await httpClient.delete(`/api/admin/users/${id}/achievements/${encodeURIComponent(code)}`)
  },

  async recheckAchievements(id: string): Promise<AdminGrantedAchievement[]> {
    const { data } = await httpClient.post<AdminGrantedAchievement[]>(
      `/api/admin/users/${id}/achievements/recheck`,
    )
    return data
  },

  async setUserTelegram(id: string, chatId: number): Promise<AdminTelegramLinkResult> {
    const { data } = await httpClient.put<AdminTelegramLinkResult>(
      `/api/admin/users/${id}/telegram`,
      { chatId },
    )
    return data
  },

  async unlinkUserTelegram(id: string): Promise<void> {
    await httpClient.delete(`/api/admin/users/${id}/telegram`)
  },

  async getStats(): Promise<AdminStats> {
    const { data } = await httpClient.get<AdminStats>('/api/admin/stats')
    return data
  },
}
