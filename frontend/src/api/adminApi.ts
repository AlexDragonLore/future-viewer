import { httpClient } from './httpClient'
import type {
  AdminFeedback,
  AdminFeedbackListResponse,
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
}
