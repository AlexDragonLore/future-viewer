import { httpClient } from './httpClient'
import type { AchievementInfo } from '@/types'

export const achievementApi = {
  async all(): Promise<AchievementInfo[]> {
    const { data } = await httpClient.get<AchievementInfo[]>('/api/achievements')
    return data
  },

  async mine(): Promise<AchievementInfo[]> {
    const { data } = await httpClient.get<AchievementInfo[]>('/api/achievements/me')
    return data
  },
}
