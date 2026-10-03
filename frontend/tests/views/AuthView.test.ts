import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import type { RegisterPayload } from '@/types'

const loginMock = vi.fn()
const registerMock = vi.fn()
const statusMock = vi.fn()

vi.mock('@/api/authApi', () => ({
  authApi: {
    login: (...args: [string, string]) => loginMock(...args),
    register: (payload: RegisterPayload) => registerMock(payload),
  },
}))

vi.mock('@/content/legal', () => ({
  legalPublication: { isVerified: false },
  loadLegalDocuments: vi.fn().mockResolvedValue(undefined),
  legalDocumentVersions: {
    offer: 'offer-v1',
    privacy: 'privacy-v1',
    personalDataConsent: 'personal-v1',
    marketingConsent: 'marketing-v1',
    cookies: 'cookies-v1',
  },
}))

vi.mock('@/api/subscriptionApi', () => ({
  subscriptionApi: {
    status: (...args: []) => statusMock(...args),
  },
}))

import AuthView from '@/views/AuthView.vue'
import { clearGuestContinuation, getGuestContinuation, saveGuestContinuation } from '@/utils/guestReading'

async function mountAuth(initialPath = '/auth'): Promise<{ wrapper: ReturnType<typeof mount>; router: Router }> {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div>home</div>' } },
      { path: '/auth', name: 'auth', component: AuthView },
      { path: '/history', component: { template: '<div>h</div>' } },
      { path: '/result', component: { template: '<div>result</div>' } },
      { path: '/forgot-password', name: 'forgot-password', component: { template: '<div>fp</div>' } },
      { path: '/legal/:document', component: { template: '<div>legal</div>' } },
    ],
  })
  router.push(initialPath)
  await router.isReady()
  const wrapper = mount(AuthView, { global: { plugins: [router] } })
  return { wrapper, router }
}

async function prepareRegistration(wrapper: ReturnType<typeof mount>) {
  await wrapper.find('button.auth-secondary-action').trigger('click')
  await wrapper.find('[data-testid="personal-data-consent-acceptance"]').setValue(true)
}

describe('AuthView', () => {
  beforeEach(() => {
    localStorage.clear()
    clearGuestContinuation()
    loginMock.mockReset()
    registerMock.mockReset()
    statusMock.mockReset()
    statusMock.mockResolvedValue({
      status: 0,
      expiresAt: null,
      isActive: false,
      freeReadingsUsedToday: 0,
      freeReadingsDailyLimit: 1,
      canCreateFreeReading: true,
    })
  })

  it('starts in login mode', async () => {
    const { wrapper } = await mountAuth()
    expect(wrapper.text()).toContain('Войти')
  })

  it('opens registration directly from the preview and keeps the continuation through verification', async () => {
    saveGuestContinuation({ ticket: 'encrypted', expiresAt: new Date(Date.now() + 86_400_000).toISOString() })
    registerMock.mockResolvedValue({ verificationRequired: true, email: 'guest@example.com', userId: 'u' })
    const { wrapper, router } = await mountAuth('/auth?mode=register&redirect=/result')
    expect(wrapper.find('h1').text()).toBe('Регистрация')
    await wrapper.find('input[type="email"]').setValue('guest@example.com')
    await wrapper.find('input[type="password"]').setValue('password123')
    await wrapper.get('[data-testid="personal-data-consent-acceptance"]').setValue(true)
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(registerMock).toHaveBeenCalled()
    expect(wrapper.text()).toContain('Затем откроется полное толкование вашей карты')
    expect(getGuestContinuation()?.ticket).toBe('encrypted')
    expect(router.currentRoute.value.path).toBe('/auth')
  })

  it('returns an existing account to its guest reading after login', async () => {
    saveGuestContinuation({ ticket: 'encrypted', expiresAt: new Date(Date.now() + 86_400_000).toISOString() })
    loginMock.mockResolvedValue({ accessToken: 't', email: 'u@x.com', expiresAt: '', userId: 'u' })
    const { wrapper, router } = await mountAuth('/auth?redirect=/result')
    await wrapper.find('input[type="email"]').setValue('u@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/result')
    expect(getGuestContinuation()?.ticket).toBe('encrypted')
  })

  it('toggles to register mode', async () => {
    const { wrapper } = await mountAuth()
    await wrapper.find('button.auth-secondary-action').trigger('click')
    expect(wrapper.text()).toContain('Регистрация')
    expect(wrapper.text()).toContain('Создать')
    expect(wrapper.text()).not.toContain('Регистрация и оплата временно недоступны')
    expect(wrapper.find('[data-testid="registration-legal-blocked"]').exists()).toBe(false)
  })

  it('calls login and redirects home on success', async () => {
    loginMock.mockResolvedValue({ accessToken: 't', email: 'u@x.com', expiresAt: '', userId: 'u' })
    const { wrapper, router } = await mountAuth()
    await wrapper.find('input[type="email"]').setValue('u@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(loginMock).toHaveBeenCalledWith('u@x.com', 'password1')
    expect(router.currentRoute.value.path).toBe('/')
  })

  it('honors redirect query parameter', async () => {
    loginMock.mockResolvedValue({ accessToken: 't', email: 'u@x.com', expiresAt: '', userId: 'u' })
    const { wrapper, router } = await mountAuth('/auth?redirect=/history')
    await wrapper.find('input[type="email"]').setValue('u@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/history')
  })

  it('shows backend validation details on failure', async () => {
    loginMock.mockRejectedValue({
      response: { data: { error: 'validation_error', details: ['Bad email', 'Bad pw'] } },
      message: 'Request failed',
    })
    const { wrapper } = await mountAuth()
    await wrapper.find('input[type="email"]').setValue('u@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(wrapper.text()).toContain('Bad email; Bad pw')
  })

  it('localizes invalid credential errors', async () => {
    loginMock.mockRejectedValue({
      isAxiosError: true,
      response: { status: 401, data: { error: 'unauthorized', message: 'Invalid credentials' } },
      message: 'Request failed',
    })
    const { wrapper } = await mountAuth()
    await wrapper.find('input[type="email"]').setValue('u@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(wrapper.text()).toContain('Неверный email или пароль')
    expect(wrapper.text()).not.toContain('Invalid credentials')
  })

  it('shows a startup hint when registration cannot reach the local API', async () => {
    registerMock.mockRejectedValue({
      isAxiosError: true,
      message: 'Network Error',
    })
    const { wrapper } = await mountAuth()
    await prepareRegistration(wrapper)
    await wrapper.find('input[type="email"]').setValue('new@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('Не удалось связаться с сервисом.')
    expect(wrapper.text()).toContain('Проверьте подключение к интернету')
    expect(wrapper.text()).not.toContain('Network Error')
  })

  it('calls register in register mode', async () => {
    registerMock.mockResolvedValue({ email: 'new@x.com', userId: 'u', verificationRequired: true })
    const { wrapper } = await mountAuth()
    await prepareRegistration(wrapper)
    await wrapper.find('input[type="email"]').setValue('new@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(registerMock).toHaveBeenCalledWith(expect.objectContaining({
      email: 'new@x.com',
      password: 'password1',
      offerAccepted: true,
      privacyAcknowledged: true,
      personalDataConsentAccepted: true,
      ageConfirmed18: true,
      optionalConsents: {
        personalization: false,
        marketing: false,
        analytics: false,
      },
      documentVersions: {
        offer: 'offer-v1',
        privacy: 'privacy-v1',
        personalDataConsent: 'personal-v1',
        marketingConsent: 'marketing-v1',
        cookies: 'cookies-v1',
      },
    }))
  })

  it('requires a distinct unchecked personal-data consent linked to its document', async () => {
    const { wrapper } = await mountAuth()
    await wrapper.find('button.auth-secondary-action').trigger('click')

    expect(wrapper.findAll('input[type="checkbox"]')).toHaveLength(1)
    expect((wrapper.get('[data-testid="personal-data-consent-acceptance"]').element as HTMLInputElement).checked).toBe(false)
    expect(wrapper.get('[data-testid="registration-terms"]').text()).toContain('возраст 18+')
    expect(wrapper.find('[data-testid="registration-terms"] a[href="/legal/offer"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="registration-terms"] a[href="/legal/privacy"]').exists()).toBe(true)

    const consentInput = wrapper.get('[data-testid="personal-data-consent-acceptance"]')
    const consentLabel = consentInput.element.closest('label')
    expect(consentLabel?.querySelector('a')?.getAttribute('href')).toBe('/legal/personal-data-consent')

    expect((wrapper.find('button[type="submit"]').element as HTMLButtonElement).disabled).toBe(true)

    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(wrapper.get('.auth-error').text()).toContain('дать согласие на обработку персональных данных')
    expect(registerMock).not.toHaveBeenCalled()

    await consentInput.setValue(true)
    expect((wrapper.find('button[type="submit"]').element as HTMLButtonElement).disabled).toBe(false)
  })

  it('omits optional choices from registration and leaves them disabled', async () => {
    const { wrapper } = await mountAuth()
    await wrapper.find('button.auth-secondary-action').trigger('click')

    for (const id of ['personalization-consent', 'telegram-consent', 'marketing-consent', 'analytics-consent']) {
      expect(wrapper.find(`[data-testid="${id}"]`).exists()).toBe(false)
    }

    await wrapper.find('[data-testid="personal-data-consent-acceptance"]').setValue(true)
    expect((wrapper.find('button[type="submit"]').element as HTMLButtonElement).disabled).toBe(false)
  })

  it('logs in and redirects after registration when verification is not required', async () => {
    registerMock.mockResolvedValue({ email: 'new@x.com', userId: 'u', verificationRequired: false })
    loginMock.mockResolvedValue({ accessToken: 't', email: 'new@x.com', expiresAt: '', userId: 'u', isAdmin: false })
    const { wrapper, router } = await mountAuth()
    await prepareRegistration(wrapper)
    await wrapper.find('input[type="email"]').setValue('new@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(loginMock).toHaveBeenCalledWith('new@x.com', 'password1')
    expect(router.currentRoute.value.path).toBe('/')
  })
})
