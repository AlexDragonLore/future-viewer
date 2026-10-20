import axios from 'axios'

export interface BirthdayQuestion {
  id: string
  numeral: string
  arcana: string
  stage: string
  title: string
  text: string
  options: { id: string; title: string; description: string }[]
}
export interface BirthdaySession {
  ticket: string
  completedSteps: number
  totalSteps: number
  score: number
  question: BirthdayQuestion | null
}
export interface BirthdayGift { amount: number; recipient: string; age: number }

// The invitation has its own session. An expired invitation must never log a
// visitor out of their regular tarot account.
const client = axios.create({ baseURL: import.meta.env.VITE_API_URL || '', timeout: 20_000 })
export const birthdayApi = {
  async unlock(key: string) {
    return (await client.post<BirthdaySession>('/api/birthday/unlock', { key })).data
  },
  async resume(ticket: string) {
    return (await client.post<BirthdaySession>('/api/birthday/session', { ticket })).data
  },
  async answer(ticket: string, questionId: string, optionId: string) {
    return (await client.post<{ correct: boolean; message: string; session: BirthdaySession }>(
      '/api/birthday/answer', { ticket, questionId, optionId })).data
  },
  async gift(ticket: string) {
    return (await client.post<BirthdayGift>('/api/birthday/gift', { ticket })).data
  },
  async asset(ticket: string, kind: 'postcard' | 'certificate') {
    return (await client.post<Blob>(`/api/birthday/assets/${kind}`, { ticket }, { responseType: 'blob' })).data
  },
}
