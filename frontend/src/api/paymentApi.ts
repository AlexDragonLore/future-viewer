import { httpClient } from './httpClient'

export interface PaymentCreation {
  paymentId: string
  confirmationUrl: string
  status: string
}

export interface PaymentOfferAcceptance {
  offerAccepted: boolean
  offerVersion: string
  tariffCode: string
}

export interface PaymentStatus {
  status: string
  paid: boolean
}

export const paymentApi = {
  async status(paymentId: string): Promise<PaymentStatus> {
    const { data } = await httpClient.get<PaymentStatus>(`/api/payments/${encodeURIComponent(paymentId)}/status`)
    return data
  },
  async createAccessPayment(acceptance: PaymentOfferAcceptance): Promise<PaymentCreation> {
    const { data } = await httpClient.post<PaymentCreation>('/api/payments/subscribe', acceptance)
    return data
  },
  async subscribe(acceptance: PaymentOfferAcceptance): Promise<PaymentCreation> {
    return paymentApi.createAccessPayment(acceptance)
  },
}
