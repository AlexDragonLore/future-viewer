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

export enum DeckType {
  RWS = 1,
  Thoth = 2,
  Marseille = 3,
  ViscontiSforza = 4,
  ModernWitch = 5,
}

export enum SuggestedTone {
  Neutral = 0,
  Supportive = 1,
  Strict = 2,
  Contemplative = 3,
}

export enum CardSuit {
  MajorArcana = 0,
  Wands = 1,
  Cups = 2,
  Swords = 3,
  Pentacles = 4,
}

export interface DeckVariantInfo {
  deckType: DeckType
  variantNote: string
}

export interface CardGlossary {
  id: number
  name: string
  nameEn: string
  suit: CardSuit
  number: number
  imagePath: string
  descriptionUpright: string
  descriptionReversed: string
  shortUpright: string
  shortReversed: string
  uprightKeywords: string[]
  reversedKeywords: string[]
  suggestedTone: SuggestedTone
  aliases: string[]
  deckVariants: DeckVariantInfo[]
}
