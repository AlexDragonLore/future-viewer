import { httpClient } from './httpClient'

export interface PaymentProduct {
  tariffCode: string
  amount: number
  currency: string
  accessDays: number
}

export interface PublicConfig {
  supportEmail: string
  paymentsEnabled: boolean
  paymentProduct?: Omit<PaymentProduct, 'tariffCode'> & { tariffCode?: string } | null
  paymentProducts?: PaymentProduct[]
}

export const publicApi = {
  async getConfig(): Promise<PublicConfig> {
    const { data } = await httpClient.get<PublicConfig>('/api/public/config')
    return data
  },
}
