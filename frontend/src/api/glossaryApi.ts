import { httpClient } from './httpClient'
import type { CardGlossary } from '@/types'

export const glossaryApi = {
  async list(): Promise<CardGlossary[]> {
    const { data } = await httpClient.get<CardGlossary[]>('/api/cards/glossary')
    return data
  },

  async get(id: number): Promise<CardGlossary> {
    const { data } = await httpClient.get<CardGlossary>(`/api/cards/${id}`)
    return data
  },
}
