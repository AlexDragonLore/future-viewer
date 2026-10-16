// Shared by the static HTML generator and SPA navigation so metadata stays consistent.
export function buildSeoStructuredData(route, context, contentData) {
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
    if (!contentData) throw new Error('Content structured data must be supplied for a content route')
    return [website, ...contentData]
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
