import { httpClient } from './httpClient'
import type { AnnouncementInfo } from '@/types'

export const announcementApi = {
  async unread(): Promise<AnnouncementInfo[]> {
    const { data } = await httpClient.get<AnnouncementInfo[]>('/api/announcements/unread')
    return data
  },

  async markRead(id: string): Promise<void> {
    await httpClient.post(`/api/announcements/${id}/read`)
  },
}
