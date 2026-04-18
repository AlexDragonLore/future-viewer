import { httpClient } from './httpClient'
import type { SubscriptionStatus } from '@/types'

export const subscriptionApi = {
  async status(): Promise<SubscriptionStatus> {
    const { data } = await httpClient.get<SubscriptionStatus>('/api/subscription/status')
    return data
  },
}
