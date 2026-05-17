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
  deckType: DeckType
}

export interface AuthResponse {
  accessToken: string
  expiresAt: string
  userId: string
  email: string
  isAdmin: boolean
}

export interface RegisterResponse {
  userId: string
  email: string
  verificationRequired: boolean
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

export enum SubscriptionStatusValue {
  None = 0,
  Active = 1,
  Expired = 2,
  Cancelled = 3,
}

export interface SubscriptionStatus {
  status: SubscriptionStatusValue
  expiresAt: string | null
  isActive: boolean
  freeReadingsUsedToday: number
  freeReadingsDailyLimit: number
  canCreateFreeReading: boolean
  tarotPlusCredits: number
}

export enum TarotPlusSessionStatus {
  Draft = 0,
  PreviewReady = 1,
  PaymentPending = 2,
  Paid = 3,
  Intake = 4,
  CardsDrawn = 5,
  ReportGenerating = 6,
  ReportReady = 7,
  Completed = 8,
  Expired = 9,
  Cancelled = 10,
}

export enum TarotPlusRoute {
  Unknown = 0,
  Relationship = 1,
  Career = 2,
  Money = 3,
  Decision = 4,
  SelfIdentity = 5,
  Family = 6,
  GeneralLife = 7,
  ResourceState = 8,
  SafetySensitive = 9,
}

export interface TarotPlusAnswer {
  kind: string
  question: string
  answer: string
  createdAt: string
}

export interface TarotPlusDrawnCard {
  position: number
  positionName: string
  cardId: number
  cardName: string
  imagePath: string
  isReversed: boolean
  meaning: string
}

export interface TarotPlusDrawnSpread {
  spreadId: string
  spreadName: string
  cards: TarotPlusDrawnCard[]
}

export interface TarotPlusFollowUpItem {
  question: string
  answerMarkdown: string
  createdAt: string
}

export interface TarotPlusSession {
  id: string
  status: TarotPlusSessionStatus
  route: TarotPlusRoute
  routeLabel: string
  coreRequest: string
  previewText: string | null
  reportMarkdown: string | null
  followUpsLeft: number
  priceRub: number
  paidAt: string | null
  createdAt: string
  updatedAt: string
  expiresAt: string
  answerCount: number
  intakeAnswerCount: number
  answers: TarotPlusAnswer[]
  drawnSpreads: TarotPlusDrawnSpread[]
  followUps: TarotPlusFollowUpItem[]
}

export interface TarotPlusSessionListItem {
  id: string
  status: TarotPlusSessionStatus
  route: TarotPlusRoute
  routeLabel: string
  coreRequest: string
  priceRub: number
  paidAt: string | null
  createdAt: string
  expiresAt: string
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

export enum FeedbackStatus {
  Pending = 0,
  Notified = 1,
  Answered = 2,
  Scored = 3,
}

export interface FeedbackInfo {
  id: string
  readingId: string
  question: string
  interpretation: string | null
  aiScore: number | null
  aiScoreReason: string | null
  isSincere: boolean | null
  selfReport: string | null
  status: FeedbackStatus
  createdAt: string
  answeredAt: string | null
}

export interface LeaderboardEntry {
  userId: string
  displayName: string
  totalScore: number
  feedbackScore: number
  achievementScore: number
  feedbackCount: number
  averageScore: number
  rank: number
}

export interface UserScoreSummary {
  totalScore: number
  feedbackScore: number
  achievementScore: number
  monthlyScore: number
  rank: number | null
  monthlyRank: number | null
  feedbackCount: number
  averageScore: number
}

export interface AchievementInfo {
  id: string
  code: string
  name: string
  description: string
  iconPath: string
  sortOrder: number
  unlockedAt: string | null
}

export interface TelegramStatus {
  isLinked: boolean
}

export interface TelegramLinkResponse {
  deepLinkUrl: string
  isLinked: boolean
}

export interface UserMemoryRule {
  id: string
  text: string
  createdAt: string
  updatedAt: string
}

export interface Personalization {
  firstName: string | null
  lastName: string | null
  birthDate: string | null
  isComplete: boolean
  memoryRules: UserMemoryRule[]
}

export interface UpdatePersonalizationPayload {
  firstName: string
  lastName: string
  birthDate: string
}
