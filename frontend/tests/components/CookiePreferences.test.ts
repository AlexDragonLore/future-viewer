import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import CookiePreferences from '@/components/CookiePreferences.vue'
import { COOKIE_POLICY_VERSION, useCookiePreferences } from '@/composables/useCookiePreferences'

beforeEach(() => {
  useCookiePreferences().resetCookiePreferences()
  localStorage.clear()
})

describe('CookiePreferences', () => {
  it('starts all optional categories off and persists the policy version', async () => {
    localStorage.clear()
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/legal/cookies', component: { template: '<div>cookies</div>' } }],
    })
    await router.push('/legal/cookies')
    await router.isReady()
    const wrapper = mount(CookiePreferences, { global: { plugins: [router] } })

    expect(wrapper.find('[data-testid="cookie-preferences"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="cookie-details"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="cookie-preferences"]').attributes('aria-modal')).toBe('false')
    await wrapper.get('.details-toggle').trigger('click')
    expect((wrapper.get('[data-testid="cookie-preferences-toggle"]').element as HTMLInputElement).checked).toBe(false)
    expect((wrapper.get('[data-testid="cookie-analytics-toggle"]').element as HTMLInputElement).checked).toBe(false)
    expect((wrapper.get('[data-testid="cookie-marketing-toggle"]').element as HTMLInputElement).checked).toBe(false)

    await wrapper.get('[data-testid="accept-necessary"]').trigger('click')
    const stored = JSON.parse(localStorage.getItem('fv_cookie_preferences_v1') ?? '{}')
    expect(stored).toMatchObject({
      version: COOKIE_POLICY_VERSION,
      necessary: true,
      preferences: false,
      analytics: false,
      marketing: false,
    })
    expect(useCookiePreferences().isAllowed('analytics')).toBe(false)
    wrapper.unmount()
  })

  it('reopens detailed settings with the saved choice so analytics can be revoked', async () => {
    const cookies = useCookiePreferences()
    cookies.acceptAll()
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/', component: { template: '<div>home</div>' } }],
    })
    await router.push('/')
    await router.isReady()
    const wrapper = mount(CookiePreferences, { global: { plugins: [router] } })
    cookies.openCookieSettings()
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="cookie-details"]').exists()).toBe(true)
    expect((wrapper.get('[data-testid="cookie-analytics-toggle"]').element as HTMLInputElement).checked).toBe(true)
    await wrapper.get('[data-testid="cookie-analytics-toggle"]').setValue(false)
    await wrapper.get('[data-testid="save-cookie-details"]').trigger('click')
    expect(cookies.isAllowed('analytics')).toBe(false)
    expect(wrapper.find('[data-testid="cookie-preferences"]').exists()).toBe(false)
    wrapper.unmount()
  })
})

describe('cross-tab storage choices', () => {
  it('applies a valid revocation from another tab without rewriting its choice', () => {
    const cookies = useCookiePreferences()
    cookies.acceptAll()
    const incoming = { ...cookies.preferences.value!, analytics: false }
    window.dispatchEvent(new StorageEvent('storage', {
      key: 'fv_cookie_preferences_v1', newValue: JSON.stringify(incoming),
    }))
    expect(cookies.isAllowed('analytics')).toBe(false)
    expect(cookies.isAllowed('preferences')).toBe(true)
    // The listener consumes the event; it does not create another storage event by writing back.
    expect(JSON.parse(localStorage.getItem('fv_cookie_preferences_v1')!).analytics).toBe(true)
  })

  it.each([null, '{invalid', JSON.stringify({ version: 'old', analytics: true })])(
    'invalid or removed choices disable optional categories (%s)', (newValue) => {
      const cookies = useCookiePreferences()
      cookies.acceptAll()
      window.dispatchEvent(new StorageEvent('storage', { key: 'fv_cookie_preferences_v1', newValue }))
      expect(cookies.isAllowed('analytics')).toBe(false)
      expect(cookies.preferences.value).toBeNull()
      expect(cookies.settingsOpen.value).toBe(true)
    },
  )

  it('ignores unrelated local storage changes', () => {
    const cookies = useCookiePreferences()
    cookies.acceptAll()
    window.dispatchEvent(new StorageEvent('storage', { key: 'fv_token', newValue: null }))
    expect(cookies.isAllowed('analytics')).toBe(true)
  })
})

describe('expired storage choices', () => {
  it('requires a fresh choice for the previous technical-only analytics permission', () => {
    const preferences = useCookiePreferences()
    localStorage.setItem('fv_cookie_preferences_v1', JSON.stringify({
      version: 'unpublished-technical-2026-08-01', necessary: true,
      preferences: true, analytics: true, marketing: true,
      decidedAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 10000).toISOString(),
    }))
    preferences.loadCookiePreferences()
    expect(preferences.isAllowed('analytics')).toBe(false)
    expect(preferences.settingsOpen.value).toBe(true)
    expect(localStorage.getItem('fv_cookie_preferences_v1')).toBeNull()
  })

  it('removes optional storage when the stored policy version changes', () => {
    const preferences = useCookiePreferences()
    preferences.resetCookiePreferences()
    localStorage.setItem('fv_deck', '1')
    localStorage.setItem('fv_cookie_preferences_v1', JSON.stringify({
      version: 'outdated-version', necessary: true, preferences: true, analytics: true, marketing: true,
      decidedAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 10000).toISOString(),
    }))
    preferences.loadCookiePreferences()
    expect(preferences.isAllowed('preferences')).toBe(false)
    expect(localStorage.getItem('fv_deck')).toBeNull()
    expect(localStorage.getItem('fv_cookie_preferences_v1')).toBeNull()
  })

  it('stops optional storage after a choice expires in the current session', () => {
    const preferences = useCookiePreferences()
    preferences.acceptAll()
    localStorage.setItem('fv_deck', '1')
    preferences.preferences.value!.expiresAt = new Date(Date.now() - 1000).toISOString()
    expect(preferences.isAllowed('preferences')).toBe(false)
    expect(localStorage.getItem('fv_deck')).toBeNull()
    expect(preferences.settingsOpen.value).toBe(true)
  })
})
