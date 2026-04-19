import { httpClient } from './httpClient'
import type { TelegramLinkResponse, TelegramStatus } from '@/types'

export const telegramApi = {
  async link(): Promise<TelegramLinkResponse> {
    const { data } = await httpClient.post<TelegramLinkResponse>('/api/telegram/link')
    return data
  },

  async unlink(): Promise<void> {
    await httpClient.delete('/api/telegram/link')
  },

  async status(): Promise<TelegramStatus> {
    const { data } = await httpClient.get<TelegramStatus>('/api/telegram/status')
    return data
  },
}
