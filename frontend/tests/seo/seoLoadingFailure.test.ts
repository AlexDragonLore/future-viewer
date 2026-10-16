import { expect, it, vi } from 'vitest'
import type { RouteLocationNormalizedLoaded } from 'vue-router'
import { applyRouteSeo } from '@/seo/applySeo'

vi.mock('@/data/tarotSeoCatalog.js', () => { throw new Error('Simulated unavailable content chunk') })

it('keeps shell/private metadata available and safely handles a failed optional content chunk', async () => {
  const route = (name: string, path: string) => ({ name, path, meta: {} }) as RouteLocationNormalizedLoaded
  await applyRouteSeo(route('home', '/'))
  expect(document.title).toContain('бесплатно без регистрации')
  expect(document.head.querySelector('#seo-managed-jsonld')).not.toBeNull()
  await expect(applyRouteSeo(route('faq', '/faq'))).resolves.toBeUndefined()
  expect(document.head.querySelector<HTMLMetaElement>('meta[name="robots"]')?.content).toBe('noindex, nofollow')
  expect(document.head.querySelector('#seo-managed-jsonld')).toBeNull()
  expect(document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')?.href).toBe('https://alex-taro.ru/faq')
})
