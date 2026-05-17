import { httpClient } from './httpClient'
import type { PaymentCreation } from './paymentApi'
import type { TarotPlusSession, TarotPlusSessionListItem } from '@/types'

const fastAiTimeoutMs = 12_000
const followUpTimeoutMs = 60_000
const reportTimeoutMs = 45_000

export interface CreateTarotPlusPreviewPayload {
  coreRequest: string
  mainSphere: string
  desiredOutcome: string
}

export interface TarotPlusPreview {
  session: TarotPlusSession
  previewText: string
  route: number
  routeLabel: string
}

export interface AddTarotPlusAnswerPayload {
  question: string
  answer: string
}

export interface TarotPlusNextStep {
  status: number
  question: string | null
  canGenerateReport: boolean
  answerCount: number
  requiredAnswers: number
  maxAnswers: number
}

export interface TarotPlusReport {
  session: TarotPlusSession
  reportMarkdown: string
}

export interface TarotPlusFollowUp {
  session: TarotPlusSession
  answerMarkdown: string
  followUpsLeft: number
}

export const tarotPlusApi = {
  async createPreview(payload: CreateTarotPlusPreviewPayload): Promise<TarotPlusPreview> {
    const { data } = await httpClient.post<TarotPlusPreview>('/api/tarot-plus/preview', payload, {
      timeout: fastAiTimeoutMs,
    })
    return data
  },

  async createPayment(sessionId: string): Promise<PaymentCreation> {
    const { data } = await httpClient.post<PaymentCreation>(`/api/tarot-plus/${sessionId}/payment`)
    return data
  },

  async get(sessionId: string): Promise<TarotPlusSession> {
    const { data } = await httpClient.get<TarotPlusSession>(`/api/tarot-plus/${sessionId}`)
    return data
  },

  async history(): Promise<TarotPlusSessionListItem[]> {
    const { data } = await httpClient.get<TarotPlusSessionListItem[]>('/api/tarot-plus/history')
    return data
  },

  async addAnswer(sessionId: string, payload: AddTarotPlusAnswerPayload): Promise<TarotPlusSession> {
    const { data } = await httpClient.post<TarotPlusSession>(`/api/tarot-plus/${sessionId}/answers`, payload)
    return data
  },

  async getNextStep(sessionId: string): Promise<TarotPlusNextStep> {
    const { data } = await httpClient.get<TarotPlusNextStep>(`/api/tarot-plus/${sessionId}/next-step`, {
      timeout: fastAiTimeoutMs,
    })
    return data
  },

  async generateReport(sessionId: string): Promise<TarotPlusReport> {
    const { data } = await httpClient.post<TarotPlusReport>(`/api/tarot-plus/${sessionId}/generate-report`, undefined, {
      timeout: reportTimeoutMs,
    })
    return data
  },

  async askFollowUp(sessionId: string, question: string): Promise<TarotPlusFollowUp> {
    const { data } = await httpClient.post<TarotPlusFollowUp>(`/api/tarot-plus/${sessionId}/follow-up`, {
      question,
    }, {
      timeout: followUpTimeoutMs,
    })
    return data
  },
}
