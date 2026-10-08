import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { clearGuestContinuation, clearUnlockedGuestReading, saveGuestContinuation, saveUnlockedGuestReading } from '@/utils/guestReading'

vi.mock('@/api/authApi', () => ({ authApi: { verifyEmail: vi.fn(async () => ({
  accessToken: 'verified-session', userId: 'guest-owner', email: 'qa@example.com', isAdmin: false,
})) } }))
vi.mock('@/api/subscriptionApi', () => ({ subscriptionApi: { status: vi.fn(async () => null) } }))

import VerifyEmailView from '@/views/VerifyEmailView.vue'

describe('VerifyEmailView guest continuation', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    localStorage.clear()
    clearGuestContinuation()
    clearUnlockedGuestReading()
    setActivePinia(createPinia())
  })
  afterEach(() => vi.useRealTimers())

  it('returns to the full result if another tab unlocks and removes the guest ticket during verification', async () => {
    const expiresAt = new Date(Date.now() + 86_400_000).toISOString()
    saveGuestContinuation({ ticket: 'encrypted', expiresAt })
    const router = createRouter({ history: createMemoryHistory(), routes: [
      { path: '/', component: { template: '<div>home</div>' } },
      { path: '/result', component: { template: '<div>result</div>' } },
      { path: '/verify-email', component: VerifyEmailView },
    ] })
    await router.push('/verify-email#token=email-token')
    await router.isReady()
    mount(VerifyEmailView, { global: { plugins: [router] } })
    await flushPromises()

    saveUnlockedGuestReading({ readingId: 'reading-id', ownerUserId: 'guest-owner', expiresAt })
    clearGuestContinuation()
    await vi.advanceTimersByTimeAsync(1200)
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/result')
  })
})
