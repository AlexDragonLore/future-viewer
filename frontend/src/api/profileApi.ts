import { httpClient } from './httpClient'
import type { Personalization, UpdatePersonalizationPayload } from '@/types'

export const profileApi = {
  async personalization(): Promise<Personalization> {
    const { data } = await httpClient.get<Personalization>('/api/profile/personalization')
    return data
  },

  async updatePersonalization(payload: UpdatePersonalizationPayload): Promise<Personalization> {
    const { data } = await httpClient.put<Personalization>('/api/profile/personalization', payload)
    return data
  },

  async deleteMemoryRule(id: string): Promise<void> {
    await httpClient.delete(`/api/profile/personalization/memory/${id}`)
  },

  async clearMemory(): Promise<void> {
    await httpClient.delete('/api/profile/personalization/memory')
  },
}
