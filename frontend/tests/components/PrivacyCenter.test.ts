import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'

const settingsMock = vi.fn()
const updateHistoryMock = vi.fn()
const consentsMock = vi.fn()
const revokeConsentMock = vi.fn()
const exportDataMock = vi.fn()
const deleteAllReadingsMock = vi.fn()
const requestDeletionMock = vi.fn()
const deletionStatusMock = vi.fn()

vi.mock('@/api/privacyApi', () => ({
  privacyApi: {
    settings: () => settingsMock(),
    updateHistory: (enabled: boolean) => updateHistoryMock(enabled),
    consents: () => consentsMock(),
    revokeConsent: (type: string) => revokeConsentMock(type),
    exportData: (password: string) => exportDataMock(password),
    deleteAllReadings: (password: string) => deleteAllReadingsMock(password),
    requestAccountDeletion: (password: string) => requestDeletionMock(password),
    accountDeletionStatus: () => deletionStatusMock(),
  },
}))

import PrivacyCenter from '@/components/PrivacyCenter.vue'
import { useAuthStore } from '@/stores/useAuthStore'
import { usePrivacyStore } from '@/stores/usePrivacyStore'

async function mountCenter() {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/auth', component: { template: '<div />' } },
      { path: '/profile', component: { template: '<div />' } },
      { path: '/history', component: { template: '<div />' } },
      { path: '/legal/data-request', component: { template: '<div />' } },
    ],
  })
  await router.push('/profile')
  await router.isReady()
  const wrapper = mount(PrivacyCenter, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

describe('PrivacyCenter', () => {
  beforeEach(() => {
    settingsMock.mockReset().mockResolvedValue({
      historyEnabled: false,
      ageConfirmed18: true,
      personalizationEnabled: false,
      marketingEnabled: true,
      analyticsEnabled: false,
    })
    updateHistoryMock.mockReset().mockImplementation(async (enabled: boolean) => ({
      historyEnabled: enabled,
      ageConfirmed18: true,
      personalizationEnabled: false,
      marketingEnabled: true,
      analyticsEnabled: false,
    }))
    consentsMock.mockReset().mockResolvedValue([
      {
        id: 'c1',
        consentType: 'Marketing',
        documentVersion: 'marketing-v1',
        acceptedAt: '2026-08-01T00:00:00Z',
        revokedAt: null,
        collectionSource: 'registration',
      },
    ])
    revokeConsentMock.mockReset().mockResolvedValue(undefined)
    exportDataMock.mockReset().mockResolvedValue(new Blob(['{}'], { type: 'application/json' }))
    deleteAllReadingsMock.mockReset().mockResolvedValue(undefined)
    requestDeletionMock.mockReset().mockResolvedValue({ requested: true, status: 'Scheduled' })
    deletionStatusMock.mockReset().mockResolvedValue({ requested: false, status: null })
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:test') })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() })
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
  })

  it('changes history and revokes each optional consent independently', async () => {
    const wrapper = await mountCenter()
    await wrapper.get('[data-testid="history-setting"]').setValue(true)
    await flushPromises()
    expect(updateHistoryMock).toHaveBeenCalledWith(true)

    await wrapper.get('[data-testid="revoke-marketing"]').trigger('click')
    await flushPromises()
    expect(revokeConsentMock).toHaveBeenCalledWith('marketing')
    expect(wrapper.text()).toContain('Не предоставлено или отозвано')
  })

  it('offers revocation when an older revoked record precedes the active consent', async () => {
    const wrapper = await mountCenter()
    const privacy = usePrivacyStore()
    privacy.consents.unshift({ ...privacy.consents[0], id: 'older', revokedAt: '2026-08-01T01:00:00Z' })
    await flushPromises()
    expect(wrapper.find('[data-testid="revoke-marketing"]').exists()).toBe(true)
  })

  it('requires re-authentication and sends the password only in API bodies', async () => {
    const wrapper = await mountCenter()
    await wrapper.get('[data-testid="export-data"]').trigger('click')
    expect(exportDataMock).not.toHaveBeenCalled()
    expect(wrapper.get('[data-testid="privacy-action-error"]').text()).toContain('текущий пароль')

    await wrapper.get('[data-testid="privacy-password"]').setValue('current-password')
    await wrapper.get('[data-testid="export-data"]').trigger('click')
    await flushPromises()
    expect(exportDataMock).toHaveBeenCalledWith('current-password')
  })

  it('starts all-history and account deletion through dedicated APIs', async () => {
    const wrapper = await mountCenter()
    await wrapper.get('[data-testid="privacy-password"]').setValue('current-password')
    await wrapper.get('[data-testid="delete-all-readings"]').trigger('click')
    await flushPromises()
    expect(deleteAllReadingsMock).toHaveBeenCalledWith('current-password')

    await wrapper.get('[data-testid="privacy-password"]').setValue('current-password')
    await wrapper.get('[data-testid="request-account-deletion"]').trigger('click')
    await flushPromises()
    expect(requestDeletionMock).toHaveBeenCalledWith('current-password')
    expect(useAuthStore().isAuthenticated).toBe(false)
    expect(localStorage.getItem('fv_token')).toBeNull()
    expect(wrapper.vm.$router.currentRoute.value.fullPath).toBe('/auth?accountDeletion=requested')
  })
})
