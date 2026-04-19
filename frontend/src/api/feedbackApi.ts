import { httpClient } from './httpClient'
import type { FeedbackInfo } from '@/types'

export interface FeedbackSubmitResult {
  score: number
  reason: string
  isSincere: boolean
}

export const feedbackApi = {
  async getByToken(token: string): Promise<FeedbackInfo> {
    const { data } = await httpClient.get<FeedbackInfo>(`/api/feedbacks/${encodeURIComponent(token)}`)
    return data
  },

  async submit(token: string, selfReport: string): Promise<FeedbackSubmitResult> {
    const { data } = await httpClient.post<FeedbackSubmitResult>(
      `/api/feedbacks/${encodeURIComponent(token)}`,
      { selfReport },
    )
    return data
  },

  async getMy(): Promise<FeedbackInfo[]> {
    const { data } = await httpClient.get<FeedbackInfo[]>('/api/feedbacks/my')
    return data
  },
}
