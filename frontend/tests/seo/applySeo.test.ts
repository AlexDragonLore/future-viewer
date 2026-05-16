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
