import { httpClient } from './httpClient'
import type { AuthResponse } from '@/types'

export const authApi = {
  async register(email: string, password: string): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>('/api/auth/register', { email, password })
    return data
  },

  async login(email: string, password: string): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>('/api/auth/login', { email, password })
    return data
  },
}
