import { buildStructuredDataForRoute } from '../data/tarotSeoCatalog.js'

// Shared by the static HTML generator and SPA navigation so metadata stays consistent.
export function buildSeoStructuredData(route, context) {
  if (!route) return []

  const siteUrl = context.siteUrl.replace(/\/+$/, '')
  const website = {
    '@context': 'https://schema.org',
    '@type': 'WebSite',
    name: context.siteName,
    url: `${siteUrl}/`,
    inLanguage: 'ru-RU',
  }

  if (route.contentKind) {
    return [website, ...buildStructuredDataForRoute(route, context)]
  }

  return [website, {
    '@context': 'https://schema.org',
    '@type': route.name === 'about' ? 'AboutPage' : route.name === 'glossary' ? 'CollectionPage' : 'WebPage',
    name: route.title,
    description: route.description,
    url: `${siteUrl}${route.path}`,
    inLanguage: 'ru-RU',
    isPartOf: {
      '@type': 'WebSite',
      name: context.siteName,
      url: `${siteUrl}/`,
    },
  }]
}
