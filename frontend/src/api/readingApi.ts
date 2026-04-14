import { httpClient } from './httpClient'
import type { Reading, SpreadType, SpreadInfo } from '@/types'

export const readingApi = {
  async create(spreadType: SpreadType, question: string): Promise<Reading> {
    const { data } = await httpClient.post<Reading>('/api/readings', { spreadType, question })
    return data
  },

  async get(id: string): Promise<Reading> {
    const { data } = await httpClient.get<Reading>(`/api/readings/${id}`)
    return data
  },

  async history(): Promise<Reading[]> {
    const { data } = await httpClient.get<Reading[]>('/api/readings/history')
    return data
  },

  async spreads(): Promise<SpreadInfo[]> {
    const { data } = await httpClient.get<SpreadInfo[]>('/api/cards/spreads')
    return data
  },
}
