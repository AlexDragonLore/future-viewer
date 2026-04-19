import type { FeedbackStatus } from '@/types'

export interface AdminFeedback {
  id: string
  readingId: string
  userId: string
  userEmail: string | null
  question: string | null
  token: string
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
