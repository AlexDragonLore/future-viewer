import { httpClient } from './httpClient'

export interface PaymentCreation {
  paymentId: string
  confirmationUrl: string
  status: string
}

export const paymentApi = {
  async subscribe(): Promise<PaymentCreation> {
    const { data } = await httpClient.post<PaymentCreation>('/api/payments/subscribe')
    return data
  },
}
