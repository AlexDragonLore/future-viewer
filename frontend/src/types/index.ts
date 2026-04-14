export enum SpreadType {
  SingleCard = 1,
  ThreeCard = 3,
  CelticCross = 10,
}

export interface CardPosition {
  index: number
  name: string
  meaning: string
}

export interface SpreadInfo {
  type: SpreadType
  name: string
  cardCount: number
  positions: CardPosition[]
}

export interface ReadingCard {
  position: number
  positionName: string
  positionMeaning: string
  cardId: number
  cardName: string
  imagePath: string
  isReversed: boolean
  meaning: string
}

export interface Reading {
  id: string
  spreadType: SpreadType
  spreadName: string
  question: string
  createdAt: string
  cards: ReadingCard[]
  interpretation: string | null
}

export interface AuthResponse {
  accessToken: string
  expiresAt: string
  userId: string
  email: string
}

export interface TarotCardInfo {
  id: number
  name: string
  imagePath: string
}
