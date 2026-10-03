import { httpClient } from './httpClient'

export type OptionalConsentType = 'personalization' | 'marketing' | 'analytics'

export interface PrivacySettings {
  historyEnabled: boolean
  ageConfirmed18: boolean
  personalizationEnabled: boolean
  marketingEnabled: boolean
  analyticsEnabled: boolean
}

export interface UserConsentRecord {
  id: string
  consentType: string
  documentVersion: string
  contentHash?: string | null
  acceptedAt: string
  revokedAt: string | null
  collectionSource: string
}

export interface AccountDeletionStatus {
  requested: boolean
  status?: string | null
  requestedAt?: string | null
  scheduledAt?: string | null
  startedAt?: string | null
  completedAt?: string | null
}

export const privacyApi = {
  async settings(): Promise<PrivacySettings> {
    const { data } = await httpClient.get<PrivacySettings>('/api/privacy/settings')
    return data
  },

  async updateHistory(enabled: boolean): Promise<PrivacySettings> {
    const { data } = await httpClient.put<PrivacySettings>('/api/privacy/settings/history', { enabled })
    return data
  },

  async consents(): Promise<UserConsentRecord[]> {
    const { data } = await httpClient.get<UserConsentRecord[]>('/api/privacy/consents')
    return data
  },

  async revokeConsent(type: OptionalConsentType): Promise<void> {
    await httpClient.post(`/api/privacy/consents/${type}/revoke`)
  },

  async exportData(password: string): Promise<Blob> {
    const { data } = await httpClient.post('/api/privacy/export', { password }, { responseType: 'blob' })
    return data as Blob
  },

  async deleteReading(id: string, password: string): Promise<void> {
    await httpClient.delete(`/api/privacy/readings/${encodeURIComponent(id)}`, { data: { password } })
  },

  async deleteAllReadings(password: string): Promise<void> {
    await httpClient.delete('/api/privacy/readings', { data: { password } })
  },

  async requestAccountDeletion(password: string): Promise<AccountDeletionStatus> {
    const { data } = await httpClient.post<AccountDeletionStatus>('/api/privacy/account-deletion', { password })
    return data
  },

  async accountDeletionStatus(): Promise<AccountDeletionStatus> {
    const { data } = await httpClient.get<AccountDeletionStatus>('/api/privacy/account-deletion/status')
    return data
  },
}
