import { resetOnAccountChange } from '@/utils/accountSession'
import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  privacyApi,
  type AccountDeletionStatus,
  type OptionalConsentType,
  type PrivacySettings,
  type UserConsentRecord,
} from '@/api/privacyApi'
import { extractApiError } from '@/api/httpClient'

export const usePrivacyStore = defineStore('privacy', () => {
  const settings = ref<PrivacySettings>({
    historyEnabled: false,
    ageConfirmed18: false,
    personalizationEnabled: false,
    marketingEnabled: false,
    analyticsEnabled: false,
  })
  const consents = ref<UserConsentRecord[]>([])
  const deletionStatus = ref<AccountDeletionStatus | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  resetOnAccountChange({ settings, consents, deletionStatus, loading, error })

  async function loadSettings() {
    settings.value = await privacyApi.settings()
  }

  async function loadConsents() {
    consents.value = await privacyApi.consents()
  }

  async function loadDeletionStatus() {
    const status = await privacyApi.accountDeletionStatus()
    deletionStatus.value = status.requested ? status : null
  }

  async function loadAll() {
    loading.value = true
    error.value = null
    const outcomes = await Promise.allSettled([loadSettings(), loadConsents(), loadDeletionStatus()])
    const failed = outcomes.find((outcome) => outcome.status === 'rejected')
    if (failed?.status === 'rejected') error.value = extractApiError(failed.reason, 'Не удалось загрузить настройки данных')
    loading.value = false
  }

  async function updateHistory(enabled: boolean) {
    settings.value = await privacyApi.updateHistory(enabled)
  }

  async function revoke(type: OptionalConsentType) {
    await privacyApi.revokeConsent(type)
    const normalized = type.toLowerCase()
    const setting = `${type}Enabled` as keyof Pick<PrivacySettings,
      'personalizationEnabled' | 'marketingEnabled' | 'analyticsEnabled'>
    settings.value[setting] = false
    consents.value = consents.value.map((consent) =>
      consent.consentType.toLowerCase() === normalized && !consent.revokedAt
        ? { ...consent, revokedAt: new Date().toISOString() }
        : consent,
    )
  }

  return {
    settings,
    consents,
    deletionStatus,
    loading,
    error,
    loadSettings,
    loadConsents,
    loadDeletionStatus,
    loadAll,
    updateHistory,
    revoke,
  }
})
