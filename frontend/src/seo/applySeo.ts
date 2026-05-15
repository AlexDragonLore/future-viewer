import type { RouteLocationNormalizedLoaded } from 'vue-router'
import seoData from './routes.json'

interface SeoRoute {
  name: string
  path: string
  title: string
  description?: string
  type?: string
}

interface SeoData {
  siteName: string
  defaultLocale: string
  siteUrl: string
  defaultImage: string
  defaultTitle: string
  defaultDescription: string
  indexableRoutes: SeoRoute[]
  noindexRoutes: SeoRoute[]
}

const config = seoData as SeoData

function cleanSiteUrl(value: string | undefined): string {
  const fallback = config.siteUrl
  const raw = typeof value === 'string' && value.trim() ? value.trim() : fallback
  return raw.replace(/\/+$/, '')
}

function absoluteUrl(path: string): string {
  const siteUrl = cleanSiteUrl(import.meta.env.VITE_SITE_URL)
  if (/^https?:\/\//i.test(path)) return path
  return `${siteUrl}${path.startsWith('/') ? path : `/${path}`}`
}

function routeByName(routes: SeoRoute[], name: unknown): SeoRoute | undefined {
  if (typeof name !== 'string') return undefined
  return routes.find((route) => route.name === name)
}

function setMeta(selector: string, create: () => HTMLMetaElement, content: string | null) {
  let el = document.head.querySelector<HTMLMetaElement>(selector)
  if (!content) {
    el?.remove()
    return
  }
  if (!el) {
    el = create()
    document.head.appendChild(el)
  }
  el.content = content
}

function setMetaName(name: string, content: string | null) {
  setMeta(
    `meta[name="${name}"]`,
    () => {
      const el = document.createElement('meta')
      el.setAttribute('name', name)
      return el
    },
    content,
  )
}

function setMetaProperty(property: string, content: string | null) {
  setMeta(
    `meta[property="${property}"]`,
    () => {
      const el = document.createElement('meta')
      el.setAttribute('property', property)
      return el
    },
    content,
  )
}

function setCanonical(url: string) {
  let el = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')
  if (!el) {
    el = document.createElement('link')
    el.rel = 'canonical'
    document.head.appendChild(el)
  }
  el.href = url
}

function setVerificationTags() {
  const google = import.meta.env.VITE_GOOGLE_SITE_VERIFICATION
  const yandex = import.meta.env.VITE_YANDEX_VERIFICATION
  setMetaName('google-site-verification', typeof google === 'string' && google.trim() ? google.trim() : null)
  setMetaName('yandex-verification', typeof yandex === 'string' && yandex.trim() ? yandex.trim() : null)
}

export function applyRouteSeo(route: RouteLocationNormalizedLoaded) {
  if (typeof document === 'undefined') return

  const indexRoute = routeByName(config.indexableRoutes, route.name)
  const noindexRoute = routeByName(config.noindexRoutes, route.name)
  const shouldIndex = !!indexRoute && route.meta.noindex !== true
  const seoRoute = indexRoute ?? noindexRoute
  const title = seoRoute?.title ?? config.defaultTitle
  const description = seoRoute?.description ?? config.defaultDescription
  const canonical = absoluteUrl(shouldIndex ? seoRoute?.path ?? route.path : route.path)
  const image = absoluteUrl(config.defaultImage)
  const robots = shouldIndex
    ? 'index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1'
    : 'noindex, nofollow'

  document.title = title
  setCanonical(canonical)
  setMetaName('description', description)
  setMetaName('robots', robots)
  setMetaProperty('og:site_name', config.siteName)
  setMetaProperty('og:locale', config.defaultLocale)
  setMetaProperty('og:type', seoRoute?.type ?? 'website')
  setMetaProperty('og:title', title)
  setMetaProperty('og:description', description)
  setMetaProperty('og:url', canonical)
  setMetaProperty('og:image', image)
  setMetaProperty('og:image:width', '1200')
  setMetaProperty('og:image:height', '630')
  setMetaName('twitter:card', 'summary_large_image')
  setMetaName('twitter:title', title)
  setMetaName('twitter:description', description)
  setMetaName('twitter:image', image)
  setVerificationTags()
}
