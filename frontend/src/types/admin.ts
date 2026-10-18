import type { DeckType, SpreadType, SubscriptionStatusValue } from '@/types'

export interface AdminUserListItem {
  id: string
  email: string
  createdAt: string
  isAdmin: boolean
  subscriptionStatus: SubscriptionStatusValue
  subscriptionExpiresAt: string | null
  subscription?: AdminSubscription | null
  totalReadings: number
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
  interpretation?: string | null
  hasInterpretation?: boolean
  deletedFromHistoryAt: string | null
}

export interface AdminReading extends AdminReadingSummary {
  userId: string | null
  userEmail: string | null
  expiresAt: string | null
  subscription?: AdminSubscription | null
}

export interface AdminSubscription {
  status: SubscriptionStatusValue
  isActive: boolean
  expiresAt: string | null
  lastPurchase: {
    tariffCode: string
    accessDays: number
    amount: number
    currency: string
    completedAt: string
  } | null
}

export interface AdminReadingListResponse {
  items: AdminReading[]
  total: number
}

export type AdminReadingAudience = 'all' | 'subscribers' | 'registered' | 'guests'

export interface ReadingSearchFilters {
  unseenOnly?: boolean
  sort?: 'newest' | 'oldest'
  audience?: AdminReadingAudience
  includeInterpretation?: boolean
  search?: string | null
  userId?: string | null
  page?: number
  pageSize?: number
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
  subscription?: AdminSubscription | null
  yukassaSubscriptionId: string | null
  totalReadings: number
  recentReadings: AdminReadingSummary[]
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

export interface AdminGrantedAchievement {
  id: string
  code: string
  name: string
  description: string
  iconPath: string
  sortOrder: number
  unlockedAt: string | null
}


export interface AdminStats {
  totalUsers: number
  adminCount: number
  activeSubscriptions: number
  readingsToday: number
  readingsThisWeek: number
}
