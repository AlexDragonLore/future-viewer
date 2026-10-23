import { httpClient } from './httpClient'
import type {
  AdminPurchaseListResponse,
  PurchaseSearchFilters,
  AdminGrantedAchievement,
  AdminReadingListResponse,
  AdminStats,
  AdminUserDetail,
  AdminUserListItem,
  AdminUserListResponse,
  ReadingSearchFilters,
  SetSubscriptionPayload,
  UserSearchFilters,
} from '@/types/admin'

export const adminApi = {
  async listPurchases(filters: PurchaseSearchFilters = {}, signal?: AbortSignal): Promise<AdminPurchaseListResponse> {
    const { data } = await httpClient.get<AdminPurchaseListResponse>('/api/admin/purchases', {
      signal,
      params: {
        search: filters.search || undefined,
        userId: filters.userId || undefined,
        status: filters.status ?? 'paid',
        page: filters.page ?? 1,
        pageSize: filters.pageSize ?? 20,
      },
    })
    return data
  },

  async listReadings(filters: ReadingSearchFilters = {}, signal?: AbortSignal): Promise<AdminReadingListResponse> {
    const { data } = await httpClient.get<AdminReadingListResponse>('/api/admin/readings', {
      signal,
      params: {
        includeInterpretation: filters.includeInterpretation ?? false,
        audience: filters.audience ?? 'all',
        unseenOnly: filters.unseenOnly ?? false,
        sort: filters.sort ?? 'newest',
        search: filters.search || undefined,
        userId: filters.userId || undefined,
        page: filters.page ?? 1,
        pageSize: filters.pageSize ?? 20,
      },
    })
    return data
  },

  async markReadingsViewed(readingIds: string[], signal?: AbortSignal): Promise<void> {
    await httpClient.post('/api/admin/readings/viewed', { readingIds }, { signal })
  },

  async getReadingInterpretation(id: string, signal?: AbortSignal): Promise<{ id: string; interpretation: string | null }> {
    const { data } = await httpClient.get<{ id: string; interpretation: string | null }>(`/api/admin/readings/${id}/interpretation`, { signal })
    return data
  },

  async listUsers(filters: UserSearchFilters = {}, signal?: AbortSignal): Promise<AdminUserListResponse> {
    const { data } = await httpClient.get<AdminUserListResponse>('/api/admin/users', {
      signal,
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

  async getStats(): Promise<AdminStats> {
    const { data } = await httpClient.get<AdminStats>('/api/admin/stats')
    return data
  },
}
