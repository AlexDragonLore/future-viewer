import type { DeckType, SpreadType } from '@/types'

export interface ReadingHistorySummary {
  id: string
  spreadType: SpreadType
  spreadName: string
  question: string
  createdAt: string
  deckType: DeckType
  interpretationPreview: string | null
}

export interface ReadingHistoryPage {
  items: ReadingHistorySummary[]
  total: number
  page: number
  pageSize: number
}
