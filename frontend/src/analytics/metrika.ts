import { watch } from 'vue'
import type { Router } from 'vue-router'
import { useCookiePreferences } from '@/composables/useCookiePreferences'
import { sanitizePageUrl, sanitizeReferrer } from './safeUrls'

export const ANALYTICS_GOALS = [
  'guest_reading_started', 'guest_preview_viewed', 'registration_started',
  'registration_submitted', 'email_verified', 'guest_reading_unlocked',
  'payment_started', 'payment_completed',
] as const

export type AnalyticsGoal = typeof ANALYTICS_GOALS[number]
type Ym = ((counter: number, method: string, ...args: unknown[]) => void) & { a?: unknown[][]; l?: number }
type AnalyticsWindow = Window & { ym?: Ym }
type PendingGoal = { goal: AnalyticsGoal; dedupKey?: string }
type DedupEntry = { key: string; expires: number }
type RouterForAnalytics = Pick<Router, 'currentRoute' | 'afterEach'>

const SCRIPT_ID = 'fv-yandex-metrika'
export const GOAL_DEDUP_STORAGE_KEY = 'fv_analytics_goals_v1'
const GOAL_TTL = 48 * 60 * 60 * 1000
const MAX_GOALS = 128
let current: AnalyticsClient | undefined

function configuredCounter(): number | undefined {
  const raw = String(import.meta.env.VITE_YANDEX_METRICA_ID ?? '')
  if (!/^[1-9]\d{0,11}$/.test(raw)) return
  const value = Number(raw)
  return Number.isSafeInteger(value) ? value : undefined
}

function hashOpaqueKey(goal: AnalyticsGoal, key: string): string {
  // The deduplication key stays in this browser. It is never part of a goal's arguments.
  let first = 2166136261
  let second = 5381
  for (const char of `${goal}:${key}`) {
    first = Math.imul(first ^ char.charCodeAt(0), 16777619)
    second = Math.imul(second, 33) ^ char.charCodeAt(0)
  }
  return `${goal}:${(first >>> 0).toString(16)}${(second >>> 0).toString(16)}`
}

function readDedup(): DedupEntry[] {
  try {
    const raw: unknown = JSON.parse(localStorage.getItem(GOAL_DEDUP_STORAGE_KEY) ?? '[]')
    if (!Array.isArray(raw)) return []
    return raw.filter((item): item is DedupEntry => item && typeof item.key === 'string'
      && item.key.length < 100 && typeof item.expires === 'number' && item.expires > Date.now())
      .slice(-MAX_GOALS)
  } catch {
    return []
  }
}

function removeAnalyticsStorage() {
  try {
    for (const storage of [localStorage, sessionStorage]) {
      const keys = Array.from({ length: storage.length }, (_, index) => storage.key(index))
      for (const key of keys) {
        if (key && (key === GOAL_DEDUP_STORAGE_KEY || key.startsWith('_ym'))) storage.removeItem(key)
      }
    }
  } catch {
    // Storage restrictions must not break registration or reading.
  }
  try {
    const names = document.cookie.split(';').map((cookie) => cookie.trim().split('=')[0])
      .filter((name) => name.startsWith('_ym_'))
    const hostParts = window.location.hostname.split('.')
    const domains = ['', ...hostParts.map((_, index) => `.${hostParts.slice(index).join('.')}`)]
    const pathParts = window.location.pathname.split('/').filter(Boolean)
    const paths = ['/', ...pathParts.map((_, index) => `/${pathParts.slice(0, index + 1).join('/')}`)]
    for (const name of names) {
      for (const domain of domains) {
        for (const path of paths) {
          document.cookie = `${name}=; Max-Age=0; path=${path}${domain ? `; domain=${domain}` : ''}`
        }
      }
    }
  } catch {
    // Cross-domain and HttpOnly cookies are not accessible to website JavaScript.
  }
}

class AnalyticsClient {
  private readonly cookies = useCookiePreferences()
  private readonly counter = configuredCounter()
  private readonly origin = window.location.origin
  private landing: string | undefined = sanitizePageUrl(window.location.href, this.origin, true)
  private landingReferrer = sanitizeReferrer(document.referrer, this.origin)
  private previousUrl: string | undefined
  private script?: HTMLScriptElement
  private removeRouteHook?: () => void
  private stopConsentWatch?: () => void
  private initialized = false
  private ready = false
  private loading = false
  private failed = false
  private disposed = false
  private pending: PendingGoal[] = []
  private readonly sent = new Set<string>()
  private readonly initedEvent: string

  constructor(private router: RouterForAnalytics) {
    this.initedEvent = `yacounter${this.counter ?? 0}inited`
    try { this.cookies.loadCookiePreferences() } catch { /* Optional analytics cannot block app startup. */ }
    this.removeRouteHook = router.afterEach((_to, _from, failure) => {
      if (!failure) this.pageview()
    })
    this.stopConsentWatch = watch(this.cookies.preferences, (_choice, previousChoice) => {
      if (this.allowed()) this.load()
      else {
        this.disable()
        // A refusal/revocation cannot later replay the earlier landing or goals.
        if (this.cookies.preferences.value || previousChoice?.analytics) {
          this.landing = undefined
          this.landingReferrer = new URL('/', this.origin).href
          removeAnalyticsStorage()
        }
      }
    }, { immediate: true, flush: 'sync' })
  }

  private allowed(): boolean {
    try {
      return !this.disposed && this.counter !== undefined && this.cookies.isAllowed('analytics')
    } catch {
      return false
    }
  }

  private call(method: string, ...args: unknown[]): boolean {
    try {
      const ym = (window as AnalyticsWindow).ym
      if (this.counter === undefined || typeof ym !== 'function') return false
      ym(this.counter, method, ...args)
      return true
    } catch {
      return false
    }
  }

  private load() {
    if (this.ready || this.loading || !this.allowed()) return
    this.failed = false
    this.loading = true
    const script = document.createElement('script')
    this.script = script
    script.id = SCRIPT_ID
    script.async = true
    script.referrerPolicy = 'no-referrer'
    script.src = 'https://mc.yandex.ru/metrika/tag.js'
    const target = window as AnalyticsWindow
    if (typeof target.ym !== 'function') {
      const stub: Ym = (...args) => { (stub.a ??= []).push(args) }
      stub.l = Date.now()
      target.ym = stub
    }
    script.onload = () => {
      if (this.script !== script || !this.allowed()) return
      document.addEventListener(this.initedEvent, this.onReady)
      this.initialized = true
      const safeUrl = this.landing ?? this.currentUrl()
      if (!this.call('init', {
        defer: true,
        triggerEvent: true,
        webvisor: false,
        clickmap: false,
        trackLinks: false,
        accurateTrackBounce: false,
        trackHash: false,
        sendTitle: false,
        ecommerce: false,
        disableYtm: true,
        // These init options are also supported by Yandex's official tag implementation.
        url: safeUrl,
        referrer: this.landingReferrer,
      })) this.onFailure()
    }
    script.onerror = () => {
      if (this.script === script) this.onFailure()
    }
    try { document.head.append(script) } catch { this.onFailure() }
  }

  private onReady = () => {
    if (!this.initialized || !this.allowed()) return
    this.loading = false
    this.ready = true
    const landing = this.landing
    if (landing) this.sendPageview(landing, this.landingReferrer)
    this.landing = undefined
    this.pageview()
    const pending = this.pending.splice(0)
    for (const event of pending) this.send(event)
  }

  private onFailure() {
    this.failed = true
    this.disable()
  }

  private currentUrl() {
    const route = this.router.currentRoute.value
    // main.ts starts analytics while the first lazy route is still resolving.
    return sanitizePageUrl(route.matched.length ? route.fullPath : window.location.href, this.origin)
  }

  private sendPageview(url: string, referer: string) {
    if (this.call('hit', url, { referer, title: 'Future Viewer' })) this.previousUrl = url
  }

  private pageview() {
    if (!this.ready || !this.allowed()) return
    const url = this.currentUrl()
    // The initial campaign query is retained for its first hit, without a duplicate impression.
    if (this.previousUrl?.split('?')[0] === url) return
    this.sendPageview(url, this.previousUrl ?? this.landingReferrer)
  }

  private alreadySent(key: string): boolean {
    return this.sent.has(key) || readDedup().some((entry) => entry.key === key)
      || this.pending.some((event) => event.dedupKey === key)
  }

  private send(event: PendingGoal): boolean {
    if (!this.ready || !this.allowed()) return false
    if (event.dedupKey && (this.sent.has(event.dedupKey)
      || readDedup().some((entry) => entry.key === event.dedupKey))) return false
    this.pageview()
    if (!this.call('reachGoal', event.goal)) return false
    if (event.dedupKey) {
      this.sent.add(event.dedupKey)
      try {
        const entries = readDedup()
        entries.push({ key: event.dedupKey, expires: Date.now() + GOAL_TTL })
        localStorage.setItem(GOAL_DEDUP_STORAGE_KEY, JSON.stringify(entries.slice(-MAX_GOALS)))
      } catch {
        // A blocked storage still deduplicates within the current page.
      }
    }
    return true
  }

  track(goal: AnalyticsGoal, opaqueKey?: string): boolean {
    if (!ANALYTICS_GOALS.includes(goal) || !this.allowed() || this.failed) return false
    if (opaqueKey !== undefined && (!opaqueKey || opaqueKey.length > 2048)) return false
    const dedupKey = opaqueKey === undefined ? undefined : hashOpaqueKey(goal, opaqueKey)
    if (dedupKey && this.alreadySent(dedupKey)) return false
    const event: PendingGoal = { goal, dedupKey }
    if (this.ready) return this.send(event)
    if (!this.loading || this.pending.length >= MAX_GOALS) return false
    this.pending.push(event)
    return true
  }

  private disable() {
    this.ready = false
    this.loading = false
    this.pending = []
    this.sent.clear()
    document.removeEventListener(this.initedEvent, this.onReady)
    if (this.initialized) this.call('destruct')
    this.initialized = false
    if (this.script) {
      this.script.onload = null
      this.script.onerror = null
      this.script.remove()
      this.script = undefined
    }
    this.previousUrl = undefined
  }

  dispose() {
    this.stopConsentWatch?.()
    this.removeRouteHook?.()
    this.disable()
    this.disposed = true
  }
}

/** Starts a single optional counter and returns its teardown function. No requests before consent. */
export function startAnalytics(router: RouterForAnalytics): () => void {
  current?.dispose()
  const client = new AnalyticsClient(router)
  current = client
  return () => {
    client.dispose()
    if (current === client) current = undefined
  }
}

/** Conversion arguments are intentionally limited to a fixed goal name. */
export function trackGoal(goal: AnalyticsGoal): boolean {
  return current?.track(goal) ?? false
}

/** Call only after a successful operation. The opaque key is hashed locally and never transmitted. */
export function trackGoalOnce(goal: AnalyticsGoal, opaqueKey: string): boolean {
  return current?.track(goal, opaqueKey) ?? false
}
