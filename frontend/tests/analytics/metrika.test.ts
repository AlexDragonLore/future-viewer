import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import { useCookiePreferences } from '@/composables/useCookiePreferences'
import {
  GOAL_DEDUP_STORAGE_KEY, startAnalytics, trackGoal, trackGoalOnce, type AnalyticsGoal,
} from '@/analytics/metrika'

const cookies = useCookiePreferences()
const counter = 12345678
const stops: Array<() => void> = []
const ymWindow = window as Window & { ym?: (...args: unknown[]) => void }

async function setupRouter(url = '/') {
  window.history.replaceState(null, '', url)
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }],
  })
  await router.push(url)
  await router.isReady()
  return router
}

function start(router: Router) {
  const stop = startAnalytics(router)
  stops.push(stop)
  return stop
}

function tag() {
  return document.querySelector<HTMLScriptElement>('#fv-yandex-metrika')
}

function makeReady() {
  const ym = vi.fn((...args: unknown[]) => {
    if (args[1] === 'init') document.dispatchEvent(new Event(`yacounter${counter}inited`))
  })
  ymWindow.ym = ym
  tag()?.dispatchEvent(new Event('load'))
  return ym
}

beforeEach(() => {
  cookies.resetCookiePreferences()
  localStorage.clear()
  sessionStorage.clear()
  delete ymWindow.ym
  vi.stubEnv('VITE_YANDEX_METRICA_ID', String(counter))
  // Keep the real script element and its lifecycle, with no external execution/network in unit QA.
  const append = document.head.append.bind(document.head)
  vi.spyOn(document.head, 'append').mockImplementation((...nodes) => {
    for (const node of nodes) {
      if (node instanceof HTMLScriptElement) node.type = 'application/json'
    }
    append(...nodes)
  })
})

afterEach(() => {
  for (const stop of stops.splice(0)) stop()
  vi.unstubAllEnvs()
  vi.restoreAllMocks()
  document.querySelectorAll('#fv-yandex-metrika').forEach((element) => element.remove())
  delete ymWindow.ym
})

describe('optional Metrika counter', () => {
  it('does not load or retain goals before consent and after refusal', async () => {
    start(await setupRouter('/?utm_source=yandex'))
    expect(tag()).toBeNull()
    expect(trackGoal('guest_reading_started')).toBe(false)
    expect(trackGoalOnce('email_verified', 'opaque-token')).toBe(false)
    expect(localStorage.getItem(GOAL_DEDUP_STORAGE_KEY)).toBeNull()
    cookies.acceptNecessary()
    expect(tag()).toBeNull()
    cookies.acceptAll()
    const ym = makeReady()
    expect(ym.mock.calls.some((call) => call[1] === 'reachGoal')).toBe(false)
    expect(ym.mock.calls.find((call) => call[1] === 'hit')?.[2])
      .toBe(`${window.location.origin}/`)
  })

  it.each(['', '0', 'not-a-counter'])('does not load for an invalid counter (%s)', async (id) => {
    vi.stubEnv('VITE_YANDEX_METRICA_ID', id)
    cookies.acceptAll()
    start(await setupRouter())
    expect(tag()).toBeNull()
    expect(trackGoal('registration_started')).toBe(false)
  })

  it('cannot block application startup when consent storage is unavailable', async () => {
    const router = await setupRouter()
    vi.spyOn(localStorage, 'getItem').mockImplementation(() => { throw new Error('storage denied') })
    vi.spyOn(localStorage, 'removeItem').mockImplementation(() => { throw new Error('storage denied') })
    expect(() => start(router)).not.toThrow()
    expect(tag()).toBeNull()
    expect(trackGoal('registration_started')).toBe(false)
  })

  it('loads after consent with replay, automatic views, titles and link tracking disabled', async () => {
    const router = await setupRouter('/?utm_source=yandex&utm_medium=cpc&yclid=123456789012&email=private')
    vi.spyOn(document, 'referrer', 'get').mockReturnValue('https://mail.example.com/u/secret?token=private')
    start(router)
    cookies.acceptAll()
    expect(tag()?.src).toBe('https://mc.yandex.ru/metrika/tag.js')
    expect(tag()?.referrerPolicy).toBe('no-referrer')
    const ym = makeReady()
    expect(ym.mock.calls[0]).toEqual([counter, 'init', expect.objectContaining({
      defer: true, triggerEvent: true, webvisor: false, clickmap: false, trackLinks: false,
      accurateTrackBounce: false, trackHash: false, sendTitle: false, ecommerce: false, disableYtm: true,
      url: `${window.location.origin}/?utm_source=yandex&utm_medium=cpc&yclid=123456789012`,
      referrer: 'https://mail.example.com/',
    })])
    expect(ym.mock.calls.filter((call) => call[1] === 'hit')).toHaveLength(1)
    expect(ym.mock.calls[1]).toEqual([counter, 'hit',
      `${window.location.origin}/?utm_source=yandex&utm_medium=cpc&yclid=123456789012`,
      { referer: 'https://mail.example.com/', title: 'Future Viewer' },
    ])
    await router.push('/verify-email?token=SECRET&email=private@example.com#private')
    expect(ym.mock.calls.at(-1)).toEqual([counter, 'hit', `${window.location.origin}/verify-email`,
      { referer: `${window.location.origin}/?utm_source=yandex&utm_medium=cpc&yclid=123456789012`,
        title: 'Future Viewer' },
    ])
    expect(JSON.stringify(ym.mock.calls)).not.toMatch(/SECRET|private|example\.com\/(u|secret)/)
  })

  it('allows only named goals without user data, after a safe first pageview', async () => {
    cookies.acceptAll()
    start(await setupRouter('/feedback/private-token?token=private'))
    expect(trackGoal('email_verified')).toBe(true)
    expect(trackGoal('email=private@example.com' as AnalyticsGoal)).toBe(false)
    const ym = makeReady()
    expect(ym.mock.calls.map((call) => call[1])).toEqual(['init', 'hit', 'reachGoal'])
    expect(ym.mock.calls.at(-1)).toEqual([counter, 'reachGoal', 'email_verified'])
    expect(JSON.stringify(ym.mock.calls)).not.toContain('private')
  })

  it('does not invent a home impression while the initial lazy route is still resolving', async () => {
    window.history.replaceState(null, '', '/verify-email?token=private')
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/verify-email', component: { render: () => null } }],
    })
    cookies.acceptAll()
    start(router)
    const ym = makeReady()
    expect(ym.mock.calls.filter((call) => call[1] === 'hit').map((call) => call[2]))
      .toEqual([`${window.location.origin}/verify-email`])
    await router.push('/verify-email?token=private')
    expect(ym.mock.calls.filter((call) => call[1] === 'hit')).toHaveLength(1)
  })

  it('drops queued goals and late initialization when consent is revoked while loading', async () => {
    cookies.acceptAll()
    start(await setupRouter())
    const loadingTag = tag()!
    const lateLoad = loadingTag.onload!
    expect(trackGoalOnce('registration_submitted', 'opaque-registration')).toBe(true)
    cookies.acceptNecessary()
    const ym = vi.fn()
    ymWindow.ym = ym
    lateLoad.call(loadingTag, new Event('load'))
    document.dispatchEvent(new Event(`yacounter${counter}inited`))
    expect(ym).not.toHaveBeenCalled()
    expect(trackGoal('registration_submitted')).toBe(false)
    expect(localStorage.getItem(GOAL_DEDUP_STORAGE_KEY)).toBeNull()
    cookies.acceptAll()
    const nextYm = makeReady()
    expect(nextYm.mock.calls.some((call) => call[1] === 'reachGoal')).toBe(false)
  })

  it('destroys an initialized counter, clears optional storage, and suppresses later calls', async () => {
    cookies.acceptAll()
    start(await setupRouter())
    const ym = makeReady()
    expect(trackGoalOnce('email_verified', 'opaque-key')).toBe(true)
    localStorage.setItem('_ym123_ls', 'analytics')
    localStorage.setItem('fv_token', 'keep-auth')
    document.cookie = '_ym_uid=123; path=/'
    cookies.acceptNecessary()
    expect(ym.mock.calls.at(-1)).toEqual([counter, 'destruct'])
    const callsAfterRevoke = ym.mock.calls.length
    expect(trackGoal('payment_completed')).toBe(false)
    expect(localStorage.getItem(GOAL_DEDUP_STORAGE_KEY)).toBeNull()
    expect(localStorage.getItem('_ym123_ls')).toBeNull()
    expect(localStorage.getItem('fv_token')).toBe('keep-auth')
    expect(document.cookie).not.toContain('_ym_uid=')
    document.dispatchEvent(new Event(`yacounter${counter}inited`))
    expect(ym.mock.calls).toHaveLength(callsAfterRevoke)
  })

  it('cancels goals when consent is revoked after script load but before provider readiness', async () => {
    cookies.acceptAll()
    start(await setupRouter('/verify-email?token=private'))
    const ym = vi.fn()
    ymWindow.ym = ym
    tag()!.dispatchEvent(new Event('load'))
    expect(ym.mock.calls[0]?.[1]).toBe('init')
    expect(trackGoalOnce('email_verified', 'opaque-token')).toBe(true)
    cookies.acceptNecessary()
    document.dispatchEvent(new Event(`yacounter${counter}inited`))
    expect(ym.mock.calls.map((call) => call[1])).toEqual(['init', 'destruct'])
    expect(localStorage.getItem(GOAL_DEDUP_STORAGE_KEY)).toBeNull()
  })

  it('deduplicates successful operations across reloads without storing or sending their raw keys', async () => {
    cookies.acceptAll()
    const router = await setupRouter('/verify-email?token=private-verification-token')
    const stop = start(router)
    const ym = makeReady()
    expect(trackGoalOnce('email_verified', 'private-verification-token')).toBe(true)
    expect(trackGoalOnce('email_verified', 'private-verification-token')).toBe(false)
    expect(ym.mock.calls.filter((call) => call[1] === 'reachGoal')).toHaveLength(1)
    expect(localStorage.getItem(GOAL_DEDUP_STORAGE_KEY)).not.toContain('private-verification-token')
    stop()
    start(router)
    const nextYm = makeReady()
    expect(trackGoalOnce('email_verified', 'private-verification-token')).toBe(false)
    expect(nextYm.mock.calls.some((call) => call[1] === 'reachGoal')).toBe(false)
    expect(trackGoalOnce('email_verified', 'a-different-successful-operation')).toBe(true)
  })

  it('deduplicates while initializing and does not store a goal until it is sent', async () => {
    cookies.acceptAll()
    start(await setupRouter())
    expect(trackGoalOnce('guest_reading_unlocked', 'opaque-reading')).toBe(true)
    expect(trackGoalOnce('guest_reading_unlocked', 'opaque-reading')).toBe(false)
    expect(localStorage.getItem(GOAL_DEDUP_STORAGE_KEY)).toBeNull()
    const ym = makeReady()
    expect(ym.mock.calls.filter((call) => call[1] === 'reachGoal')).toHaveLength(1)
    expect(localStorage.getItem(GOAL_DEDUP_STORAGE_KEY)).not.toBeNull()
  })

  it('expires old goal markers and remains usable when analytics storage is unavailable', async () => {
    cookies.acceptAll()
    const router = await setupRouter()
    const stop = start(router)
    makeReady()
    trackGoalOnce('payment_completed', 'opaque-old-payment')
    const markers = JSON.parse(localStorage.getItem(GOAL_DEDUP_STORAGE_KEY)!) as Array<{ expires: number }>
    for (const marker of markers) marker.expires = Date.now() - 1
    localStorage.setItem(GOAL_DEDUP_STORAGE_KEY, JSON.stringify(markers))
    stop()
    start(router)
    const ym = makeReady()
    vi.spyOn(localStorage, 'setItem').mockImplementation(() => { throw new Error('storage denied') })
    expect(trackGoalOnce('payment_completed', 'opaque-old-payment')).toBe(true)
    expect(trackGoalOnce('payment_completed', 'opaque-old-payment')).toBe(false)
    expect(ym.mock.calls.filter((call) => call[1] === 'reachGoal')).toHaveLength(1)
  })

  it('fails quietly if the script cannot load, and retries only after a new consent choice', async () => {
    cookies.acceptAll()
    start(await setupRouter())
    expect(trackGoal('guest_reading_started')).toBe(true)
    tag()!.dispatchEvent(new Event('error'))
    expect(tag()).toBeNull()
    expect(trackGoal('guest_reading_started')).toBe(false)
    cookies.acceptNecessary()
    cookies.acceptAll()
    const ym = makeReady()
    expect(ym.mock.calls.some((call) => call[1] === 'reachGoal')).toBe(false)
    expect(trackGoal('guest_reading_started')).toBe(true)
  })
})
