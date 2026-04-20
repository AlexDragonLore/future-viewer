import { httpClient } from './httpClient'
import type { AuthResponse, RegisterResponse } from '@/types'

export const authApi = {
  async register(email: string, password: string): Promise<RegisterResponse> {
    const { data } = await httpClient.post<RegisterResponse>('/api/auth/register', { email, password })
    return data
  },

  async login(email: string, password: string): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>('/api/auth/login', { email, password })
    return data
  },

  async verifyEmail(token: string): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>('/api/auth/verify-email', { token })
    return data
  },

  async resendVerification(email: string): Promise<void> {
    await httpClient.post('/api/auth/resend-verification', { email })
  },

  async forgotPassword(email: string): Promise<void> {
    await httpClient.post('/api/auth/forgot-password', { email })
  },

  async resetPassword(token: string, newPassword: string): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>('/api/auth/reset-password', { token, newPassword })
    return data
  },
}
