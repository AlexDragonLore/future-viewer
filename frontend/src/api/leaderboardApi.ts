import { httpClient } from './httpClient'
import type { LeaderboardEntry, UserScoreSummary } from '@/types'

export const leaderboardApi = {
  async monthly(year?: number, month?: number, take?: number): Promise<LeaderboardEntry[]> {
    const params: Record<string, number> = {}
    if (year !== undefined) params.year = year
    if (month !== undefined) params.month = month
    if (take !== undefined) params.take = take
    const { data } = await httpClient.get<LeaderboardEntry[]>('/api/leaderboard/monthly', { params })
    return data
  },

  async alltime(take?: number): Promise<LeaderboardEntry[]> {
    const params: Record<string, number> = {}
    if (take !== undefined) params.take = take
    const { data } = await httpClient.get<LeaderboardEntry[]>('/api/leaderboard/alltime', { params })
    return data
  },

  async me(): Promise<UserScoreSummary | null> {
    const response = await httpClient.get<UserScoreSummary>('/api/leaderboard/me')
    if (response.status === 204) return null
    return response.data ?? null
  },
}
