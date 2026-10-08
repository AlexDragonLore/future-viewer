import { describe, expect, it, beforeEach } from 'vitest'
import type { RouteLocationNormalizedLoaded } from 'vue-router'
import { applyRouteSeo } from '@/seo/applySeo'

function route(name: string, path: string, noindex = false): RouteLocationNormalizedLoaded {
  return {
    name,
    path,
    fullPath: path,
    query: {},
    hash: '',
    params: {},
    matched: [],
    redirectedFrom: undefined,
    meta: noindex ? { noindex: true } : {},
  }
}

function meta(selector: string): HTMLMetaElement | null {
  return document.head.querySelector(selector)
}

describe('applyRouteSeo', () => {
  beforeEach(() => {
    document.head.innerHTML = ''
    document.title = ''
  })

  it('applies indexable metadata and canonical URL for public routes', () => {
    applyRouteSeo(route('glossary', '/glossary'))

    expect(document.title).toContain('Глоссарий Таро')
    expect(meta('meta[name="robots"]')?.content).toContain('index, follow')
    expect(meta('meta[property="og:title"]')?.content).toContain('Глоссарий Таро')
    expect(meta('meta[name="google-site-verification"]')?.content).toBe(
      'wIruS4kUKqmhYfO04Yq8VUay-fwhwo1iGnYvyL_LQMg',
    )
    expect(meta('meta[name="yandex-verification"]')?.content).toBe('3e5ca7086b4b140e')
    expect(document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')?.href).toBe(
      'https://alex-taro.ru/glossary',
    )
  })

  it('describes the free guest preview and keeps advertising parameters out of canonical metadata', () => {
    const landing = route('home', '/')
    landing.fullPath = '/?utm_source=yandex&utm_medium=cpc&utm_campaign=launch&yclid=123#start'
    landing.query = { utm_source: 'yandex', utm_medium: 'cpc', utm_campaign: 'launch', yclid: '123' }
    landing.hash = '#start'

    applyRouteSeo(landing)

    expect(document.title).toContain('бесплатно без регистрации')
    expect(meta('meta[name="description"]')?.content).toContain('половина толкования')
    expect(meta('meta[name="description"]')?.content).toContain('Первый расклад Таро на 3 карты')
    expect(meta('meta[name="description"]')?.content).toContain('одна карта в день бесплатно')
    expect(meta('meta[name="description"]')?.content).toContain('Подтвердите email')
    expect(document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')?.href).toBe('https://alex-taro.ru/')
    expect(meta('meta[property="og:url"]')?.content).toBe('https://alex-taro.ru/')
    const structured = JSON.parse(document.head.querySelector<HTMLScriptElement>('#seo-managed-jsonld')!.textContent!)
    expect(structured.map((entry: { '@type': string }) => entry['@type'])).toEqual(['WebSite', 'WebPage'])
    expect(JSON.stringify(structured)).not.toMatch(/utm_|yclid|AggregateRating|Offer|SearchAction/)
  })

  it.each([
    ['result', '/result'],
    ['profile', '/profile'],
    ['reading-detail', '/reading/7dcf-private-reading'],
    ['feedback', '/feedback/private-token'],
    ['admin-users', '/admin/users'],
    ['verify-email', '/verify-email'],
  ])('removes public structured data when navigating to %s', (name, path) => {
    applyRouteSeo(route('home', '/'))
    expect(document.head.querySelector('#seo-managed-jsonld')).not.toBeNull()

    const privateRoute = route(name, path, true)
    privateRoute.fullPath = `${path}?token=secret&utm_source=yandex`
    privateRoute.query = { token: 'secret', utm_source: 'yandex' }
    applyRouteSeo(privateRoute)

    expect(meta('meta[name="robots"]')?.content).toBe('noindex, nofollow')
    expect(document.head.querySelector('#seo-managed-jsonld')).toBeNull()
    expect(document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')?.href).not.toContain('?')
  })

  it('updates the prerendered managed JSON-LD rather than leaving stale homepage markup', () => {
    document.head.innerHTML = '<script id="seo-managed-jsonld" type="application/ld+json">[{"@type":"WebSite"}]</script>'

    applyRouteSeo(route('tarot-card-seo', '/tarot/cards/shut'))

    expect(document.head.querySelectorAll('script[type="application/ld+json"]')).toHaveLength(1)
    expect(document.head.querySelector('#seo-managed-jsonld')?.textContent).toContain('Шут')
  })

  it('marks thin and private routes as noindex', () => {
    applyRouteSeo(route('auth', '/auth', true))

    expect(document.title).toContain('Вход')
    expect(meta('meta[name="robots"]')?.content).toBe('noindex, nofollow')
    expect(meta('meta[property="og:url"]')?.content).toBe('https://alex-taro.ru/auth')
  })

  it('keeps leaderboard out of the index with a self canonical URL', () => {
    applyRouteSeo(route('leaderboard', '/leaderboard', true))

    expect(document.title).toContain('Лидерборд')
    expect(meta('meta[name="robots"]')?.content).toBe('noindex, nofollow')
    expect(document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')?.href).toBe(
      'https://alex-taro.ru/leaderboard',
    )
  })

  it('applies indexable metadata for generated tarot content routes', () => {
    applyRouteSeo(route('tarot-card-seo', '/tarot/cards/shut'))

    expect(document.title).toContain('Шут')
    expect(meta('meta[name="robots"]')?.content).toContain('index, follow')
    expect(meta('meta[property="og:url"]')?.content).toBe('https://alex-taro.ru/tarot/cards/shut')
    expect(document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')?.href).toBe(
      'https://alex-taro.ru/tarot/cards/shut',
    )
    expect(document.head.querySelector<HTMLScriptElement>('script#seo-managed-jsonld')?.textContent).toContain(
      'BreadcrumbList',
    )
  })
})
