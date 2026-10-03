const SAFE_PATHS = new Set([
  '/', '/reading', '/result', '/history', '/auth', '/verify-email', '/forgot-password',
  '/reset-password', '/glossary', '/faq', '/about', '/payment/success', '/leaderboard',
  '/achievements', '/profile', '/admin', '/admin/users', '/admin/feedbacks', '/admin/stats',
  '/legal/privacy', '/legal/personal-data-consent', '/legal/cookies', '/legal/offer',
  '/legal/marketing-consent', '/legal/ai-disclaimer', '/legal/data-request', '/legal/processors',
])

function safePath(path: string): string {
  if (SAFE_PATHS.has(path)) return path
  if (path.startsWith('/reading/')) return '/reading/detail'
  if (path.startsWith('/feedback/')) return '/feedback'
  if (path.startsWith('/admin/users/')) return '/admin/users/detail'
  if (path.startsWith('/admin/')) return '/admin'
  if (path.startsWith('/glossary/')) return '/glossary/card'
  if (path.startsWith('/tarot/cards/')) return '/tarot/cards'
  if (path.startsWith('/tarot/spreads/')) return '/tarot/spreads'
  if (path.startsWith('/tarot/decks/')) return '/tarot/decks'
  return '/other'
}

// Campaign labels are technical identifiers. Free-text search terms are deliberately omitted.
const CAMPAIGN_KEYS = ['utm_source', 'utm_medium', 'utm_campaign', 'utm_content'] as const
const TECHNICAL_LABEL = /^[a-zA-Z0-9][a-zA-Z0-9_-]{0,79}$/

/** Never pass the original route, hash, token, email, question or record ID to a counter. */
export function sanitizePageUrl(raw: string, origin: string, includeAttribution = false): string {
  try {
    const parsed = new URL(raw, origin)
    const safe = new URL(safePath(parsed.pathname), origin)
    if (includeAttribution) {
      for (const key of CAMPAIGN_KEYS) {
        const value = parsed.searchParams.get(key)
        if (value && TECHNICAL_LABEL.test(value)) safe.searchParams.set(key, value)
      }
      const yclid = parsed.searchParams.get('yclid')
      if (yclid && /^\d{8,30}$/.test(yclid)) safe.searchParams.set('yclid', yclid)
    }
    return safe.href
  } catch {
    return new URL('/other', origin).href
  }
}

/** Only the referring host is needed; paths and queries may contain private data. */
export function sanitizeReferrer(raw: string, origin: string): string {
  try {
    const parsed = new URL(raw)
    if (['http:', 'https:'].includes(parsed.protocol) && !parsed.username && !parsed.password) {
      return `${parsed.origin}/`
    }
  } catch {
    // Empty referrer must not be passed: Metrika falls back to document.referrer for empty strings.
  }
  return new URL('/', origin).href
}
