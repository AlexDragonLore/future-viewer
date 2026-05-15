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
})
