import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { SubscriptionStatusValue, type RegisterPayload, type SubscriptionStatus } from '@/types'

vi.mock('@/api/authApi', () => ({
  authApi: {
    login: vi.fn(async (email: string) => ({
      accessToken: 'jwt-token',
      expiresAt: '2030-01-01T00:00:00Z',
      userId: 'u1',
      email,
      isAdmin: email.startsWith('admin'),
    })),
    register: vi.fn(async (payload: RegisterPayload) => ({
      userId: 'u2',
      email: payload.email,
      verificationRequired: true,
    })),
    verifyEmail: vi.fn(async () => ({
      accessToken: 'jwt-new',
      expiresAt: '2030-01-01T00:00:00Z',
      userId: 'u2',
      email: 'new@example.com',
      isAdmin: false,
    })),
    resendVerification: vi.fn(async () => undefined),
    forgotPassword: vi.fn(async () => undefined),
    resetPassword: vi.fn(async () => ({
      accessToken: 'jwt-reset',
      expiresAt: '2030-01-01T00:00:00Z',
      userId: 'u3',
      email: 'reset@example.com',
      isAdmin: false,
    })),
  },
}))

vi.mock('@/api/subscriptionApi', () => ({
  subscriptionApi: {
    status: vi.fn(async () => ({ isActive: false, canCreateIntroReading: false, canCreateFreeReading: true })),
  },
}))

import { useAuthStore } from '@/stores/useAuthStore'
import { subscriptionApi } from '@/api/subscriptionApi'

describe('useAuthStore', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('is unauthenticated when no token present', () => {
    const auth = useAuthStore()
    expect(auth.token).toBeNull()
    expect(auth.isAuthenticated).toBe(false)
    expect(auth.email).toBeNull()
    expect(auth.canCreateIntroReading).toBe(false)
  })

  it('uses the server entitlement for the first reading and clears it on logout', () => {
    const auth = useAuthStore()
    auth.subscription = {
      status: SubscriptionStatusValue.None, expiresAt: null, isActive: false,
      freeReadingsUsedToday: 0, freeReadingsDailyLimit: 1,
      canCreateIntroReading: true, canCreateFreeReading: true,
    }
    expect(auth.canCreateIntroReading).toBe(true)
    auth.logout()
    expect(auth.canCreateIntroReading).toBe(false)
  })

  it('hydrates from localStorage on creation', () => {
    localStorage.setItem('fv_token', 'persisted')
    localStorage.setItem('fv_email', 'saved@example.com')
    const auth = useAuthStore()
    expect(auth.token).toBe('persisted')
    expect(auth.email).toBe('saved@example.com')
    expect(auth.isAuthenticated).toBe(true)
  })

  it('login persists token and email to localStorage', async () => {
    const auth = useAuthStore()
    await auth.login('user@example.com', 'password123')
    expect(auth.token).toBe('jwt-token')
    expect(auth.email).toBe('user@example.com')
    expect(localStorage.getItem('fv_token')).toBe('jwt-token')
    expect(localStorage.getItem('fv_email')).toBe('user@example.com')
  })

  it('register does not persist credentials and returns verification response', async () => {
    const auth = useAuthStore()
    const payload: RegisterPayload = {
      email: 'new@example.com',
      password: 'password123',
      offerAccepted: true,
      privacyAcknowledged: true,
      personalDataConsentAccepted: true,
      ageConfirmed18: true,
      documentVersions: {
        offer: '1',
        privacy: '1',
        personalDataConsent: '1',
        marketingConsent: '1',
        cookies: '1',
      },
      optionalConsents: { personalization: false, marketing: false, analytics: false },
      collectionSource: 'registration',
    }
    const response = await auth.register(payload)
    expect(response.verificationRequired).toBe(true)
    expect(response.email).toBe('new@example.com')
    expect(auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem('fv_token')).toBeNull()
  })

  it('verifyEmail persists credentials', async () => {
    const auth = useAuthStore()
    await auth.verifyEmail('some-token')
    expect(auth.isAuthenticated).toBe(true)
    expect(localStorage.getItem('fv_token')).toBe('jwt-new')
  })

  it('publishes the owner identity before the token triggers other tabs to reload', async () => {
    const originalSetItem = localStorage.setItem.bind(localStorage)
    const publishedOwners: Array<string | null> = []
    const setItem = vi.spyOn(localStorage, 'setItem').mockImplementation((key: string, value: string) => {
      if (key === 'fv_token') publishedOwners.push(localStorage.getItem('fv_user_id'))
      originalSetItem(key, value)
    })
    try {
      await useAuthStore().verifyEmail('some-token')
      expect(publishedOwners).toEqual(['u2'])
    } finally {
      setItem.mockRestore()
    }
  })

  it('logout clears store and localStorage', async () => {
    const auth = useAuthStore()
    await auth.login('user@example.com', 'password123')
    auth.logout()
    expect(auth.token).toBeNull()
    expect(auth.email).toBeNull()
    expect(auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem('fv_token')).toBeNull()
    expect(localStorage.getItem('fv_email')).toBeNull()
  })

  it('login persists isAdmin flag when returned', async () => {
    const auth = useAuthStore()
    await auth.login('admin@example.com', 'password123')
    expect(auth.isAdmin).toBe(true)
    expect(localStorage.getItem('fv_is_admin')).toBe('true')
  })

  it('non-admin login clears any stale isAdmin flag', async () => {
    localStorage.setItem('fv_is_admin', 'true')
    const auth = useAuthStore()
    await auth.login('user@example.com', 'password123')
    expect(auth.isAdmin).toBe(false)
    expect(localStorage.getItem('fv_is_admin')).toBeNull()
  })

  it('hydrates isAdmin from localStorage', () => {
    localStorage.setItem('fv_token', 'persisted')
    localStorage.setItem('fv_is_admin', 'true')
    const auth = useAuthStore()
    expect(auth.isAdmin).toBe(true)
  })

  it('logout clears isAdmin flag', async () => {
    const auth = useAuthStore()
    await auth.login('admin@example.com', 'password123')
    expect(auth.isAdmin).toBe(true)
    auth.logout()
    expect(auth.isAdmin).toBe(false)
    expect(localStorage.getItem('fv_is_admin')).toBeNull()
  })

  it('forgotPassword does not persist credentials', async () => {
    const auth = useAuthStore()
    await auth.forgotPassword('someone@example.com')
    expect(auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem('fv_token')).toBeNull()
  })

  it('resetPassword persists credentials on success', async () => {
    const auth = useAuthStore()
    await auth.resetPassword('reset-token', 'newpass12')
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.token).toBe('jwt-reset')
    expect(auth.email).toBe('reset@example.com')
    expect(localStorage.getItem('fv_token')).toBe('jwt-reset')
  })
})

describe('account data isolation', () => {
  it('clears account-specific caches on logout', async () => {
    const { usePrivacyStore } = await import('@/stores/usePrivacyStore')
    const { useProfileStore } = await import('@/stores/useProfileStore')
    const { useReadingStore } = await import('@/stores/useReadingStore')
    const { useAdminStore } = await import('@/stores/useAdminStore')
    const { SpreadType } = await import('@/types')
    const auth = useAuthStore()
    await auth.login('admin@example.com', 'password123')
    const privacy = usePrivacyStore()
    const profile = useProfileStore()
    const reading = useReadingStore()
    const admin = useAdminStore()
    privacy.settings.personalizationEnabled = true
    reading.setPending({ spreadType: SpreadType.ThreeCard, question: 'private question', questionWarningAcknowledged: false, validated: false })
    reading.streamingText = 'private interpretation'
    admin.userSearch = 'another-user@example.com'
    auth.logout()
    expect(privacy.settings.personalizationEnabled).toBe(false)
    expect(reading.pending).toBeNull()
    expect(reading.streamingText).toBe('')
    expect(admin.userSearch).toBeNull()
  })
})


describe('subscription refresh concurrency', () => {
  const value = (isActive: boolean): SubscriptionStatus => ({
    status: isActive ? SubscriptionStatusValue.Active : SubscriptionStatusValue.None,
    expiresAt: null, isActive, freeReadingsUsedToday: 0, freeReadingsDailyLimit: 1,
    canCreateIntroReading: !isActive, canCreateFreeReading: true,
  })
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    vi.mocked(subscriptionApi.status).mockReset()
  })
  it('shares one in-flight request for concurrent header and page refreshes', async () => {
    let finish!: (status: SubscriptionStatus) => void
    vi.mocked(subscriptionApi.status).mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const auth = useAuthStore()
    auth.token = 'same-session'
    const first = auth.refreshSubscription()
    const second = auth.refreshSubscription()
    expect(subscriptionApi.status).toHaveBeenCalledTimes(1)
    expect(auth.subscriptionLoading).toBe(true)
    finish(value(true))
    await Promise.all([first, second])
    expect(auth.isSubscribed).toBe(true)
    expect(auth.subscriptionLoading).toBe(false)
  })
  it('a post-mutation refresh supersedes older status and subsequent calls join the fresh request', async () => {
    let finishOld!: (status: SubscriptionStatus) => void
    let finishFresh!: (status: SubscriptionStatus) => void
    vi.mocked(subscriptionApi.status)
      .mockImplementationOnce(() => new Promise(resolve => { finishOld = resolve }))
      .mockImplementationOnce(() => new Promise(resolve => { finishFresh = resolve }))
    const auth = useAuthStore()
    auth.token = 'same-session'
    const old = auth.refreshSubscription()
    const fresh = auth.refreshSubscription({ force: true })
    const joined = auth.refreshSubscription()
    expect(subscriptionApi.status).toHaveBeenCalledTimes(2)
    finishFresh(value(true))
    await Promise.all([fresh, joined])
    finishOld(value(false))
    await old
    expect(auth.isSubscribed).toBe(true)
    expect(auth.subscriptionLoading).toBe(false)
  })
  it('allows retry after failure and does not cache completed status indefinitely', async () => {
    vi.mocked(subscriptionApi.status).mockRejectedValueOnce(new Error('network')).mockResolvedValue(value(true))
    const auth = useAuthStore()
    auth.token = 'session'
    await auth.refreshSubscription()
    expect(auth.subscription).toBeNull()
    expect(auth.subscriptionLoading).toBe(false)
    await auth.refreshSubscription()
    expect(auth.isSubscribed).toBe(true)
    await auth.refreshSubscription()
    expect(subscriptionApi.status).toHaveBeenCalledTimes(3)
  })
  it('ignores an old response after logout and a new token', async () => {
    let finishOld!: (status: SubscriptionStatus) => void
    vi.mocked(subscriptionApi.status)
      .mockImplementationOnce(() => new Promise(resolve => { finishOld = resolve }))
      .mockResolvedValueOnce(value(false))
    const auth = useAuthStore()
    auth.token = 'old-token'
    const old = auth.refreshSubscription()
    auth.logout()
    auth.token = 'new-token'
    await auth.refreshSubscription()
    finishOld(value(true))
    await old
    expect(auth.isSubscribed).toBe(false)
    expect(auth.subscriptionLoading).toBe(false)
    expect(subscriptionApi.status).toHaveBeenCalledTimes(2)
  })
  it('does not share a pending request with a different token in the same session', async () => {
    let finishOld!: (status: SubscriptionStatus) => void
    vi.mocked(subscriptionApi.status)
      .mockImplementationOnce(() => new Promise(resolve => { finishOld = resolve }))
      .mockResolvedValueOnce(value(false))
    const auth = useAuthStore()
    auth.token = 'old-token'
    const old = auth.refreshSubscription()
    auth.token = 'replacement-token'
    await auth.refreshSubscription()
    finishOld(value(true))
    await old
    expect(auth.isSubscribed).toBe(false)
    expect(subscriptionApi.status).toHaveBeenCalledTimes(2)
  })
})
