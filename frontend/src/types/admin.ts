import type {
  DeckType,
  FeedbackStatus,
  SpreadType,
  SubscriptionStatusValue,
  TarotPlusRoute,
  TarotPlusSessionStatus,
} from '@/types'

export interface AdminFeedback {
  id: string
  readingId: string
  userId: string
  userEmail: string | null
  question: string | null
  selfReport: string | null
  aiScore: number | null
  aiScoreReason: string | null
  isSincere: boolean | null
  scheduledAt: string
  notifiedAt: string | null
  answeredAt: string | null
  status: FeedbackStatus
  createdAt: string
}

export interface AdminFeedbackListResponse {
  items: AdminFeedback[]
  total: number
}

export interface CreateAdminFeedbackPayload {
  readingId: string
  scheduledAt?: string | null
  bypassDelay?: boolean
  replace?: boolean
}

export interface CreateSyntheticFeedbackPayload {
  readingId: string
  aiScore: number
  aiScoreReason?: string | null
  isSincere?: boolean
  selfReport?: string | null
}

export interface UpdateAdminFeedbackPayload {
  aiScore?: number | null
  aiScoreReason?: string | null
  isSincere?: boolean | null
  status?: FeedbackStatus
  selfReport?: string | null
  scheduledAt?: string | null
  notifiedAt?: string | null
  answeredAt?: string | null
}

export interface FeedbackSearchFilters {
  userId?: string | null
  status?: FeedbackStatus | null
  page?: number
  pageSize?: number
}

export interface RunNotificationsResult {
  processed: number
}

export interface AdminUserListItem {
  id: string
  email: string
  createdAt: string
  isAdmin: boolean
  subscriptionStatus: SubscriptionStatusValue
  subscriptionExpiresAt: string | null
  telegramChatId: number | null
  tarotPlusCredits: number
  totalReadings: number
  totalTarotPlusSessions: number
  totalFeedbacks: number
  totalScore: number
}

export interface AdminUserListResponse {
  items: AdminUserListItem[]
  total: number
}

export interface AdminReadingSummary {
  id: string
  question: string
  spreadType: SpreadType
  deckType: DeckType
  createdAt: string
}

export interface AdminAchievement {
  id: string
  code: string
  name: string
  unlockedAt: string
}

export interface AdminUserDetail {
  id: string
  email: string
  createdAt: string
  isAdmin: boolean
  subscriptionStatus: SubscriptionStatusValue
  subscriptionExpiresAt: string | null
  yukassaSubscriptionId: string | null
  telegramChatId: number | null
  hasTelegramLinkToken: boolean
  tarotPlusCredits: number
  totalReadings: number
  totalTarotPlusSessions: number
  totalFeedbacks: number
  totalScore: number
  recentReadings: AdminReadingSummary[]
  recentTarotPlusSessions: AdminTarotPlusSession[]
  recentFeedbacks: AdminFeedback[]
  achievements: AdminAchievement[]
}

export interface UserSearchFilters {
  search?: string | null
  page?: number
  pageSize?: number
}

export interface SetSubscriptionPayload {
  status: SubscriptionStatusValue
  expiresAt?: string | null
}

export interface SetTarotPlusCreditsPayload {
  credits: number
}

export interface AdminGrantedAchievement {
  id: string
  code: string
  name: string
  description: string
  iconPath: string
  sortOrder: number
  unlockedAt: string | null
}

export interface AdminTelegramLinkResult {
  linked: boolean
  chatId: number
}

export interface AdminStats {
  totalUsers: number
  adminCount: number
  activeSubscriptions: number
  readingsToday: number
  readingsThisWeek: number
  tarotPlusSessionsTotal: number
  tarotPlusPaidSessions: number
  tarotPlusReportsReady: number
  tarotPlusCreatedThisWeek: number
  pendingFeedbacksToNotify: number
  scoredFeedbacksThisMonth: number
}

export interface AdminTarotPlusSession {
  id: string
  userId: string
  userEmail: string | null
  status: TarotPlusSessionStatus
  route: TarotPlusRoute
  routeLabel: string
  coreRequest: string
  previewText: string | null
  hasReport: boolean
  answerCount: number
  intakeAnswerCount: number
  followUpsLeft: number
  priceRub: number
  paymentId: string | null
  paidAt: string | null
  aiModel: string | null
  createdAt: string
  updatedAt: string
  expiresAt: string
}

export interface AdminTarotPlusListResponse {
  items: AdminTarotPlusSession[]
  total: number
}

export interface TarotPlusSearchFilters {
  userId?: string | null
  status?: TarotPlusSessionStatus | null
  page?: number
  pageSize?: number
}
