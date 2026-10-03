import DOMPurify from 'dompurify'
import { marked } from 'marked'

const renderer = new marked.Renderer()

function escapeHtml(value: string) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;')
}

function safeLinkTarget(value: string) {
  const href = value.trim()
  if (href.startsWith('/') && !href.startsWith('//')) return href

  try {
    const parsed = new URL(href)
    return parsed.protocol === 'https:' ? parsed.toString() : null
  } catch {
    return null
  }
}

renderer.html = () => ''
renderer.link = function ({ href, title, tokens }) {
  const label = this.parser.parseInline(tokens)
  const target = safeLinkTarget(href)
  if (!target) return label

  const safeTitle = title ? ` title="${escapeHtml(title)}"` : ''
  return `<a href="${escapeHtml(target)}"${safeTitle} target="_blank" rel="noopener noreferrer nofollow">${label}</a>`
}

/**
 * Renders the small Markdown subset used by AI interpretations.
 * Raw HTML is removed before parsing and the resulting DOM is filtered again.
 */
export function safeMarkdown(source: string | null | undefined): string {
  if (!source) return ''

  const withoutRawHtml = source
    .replace(/<[^>]*(?:>|$)/gu, '')
    .replace(/\b(?:javascript|vbscript|data):/giu, '')

  const rendered = marked.parse(withoutRawHtml, {
    async: false,
    gfm: true,
    breaks: true,
    renderer,
  }) as string

  return DOMPurify.sanitize(rendered, {
    ALLOWED_TAGS: ['p', 'br', 'strong', 'em', 'ul', 'ol', 'li', 'blockquote', 'h2', 'h3', 'code', 'pre', 'a'],
    ALLOWED_ATTR: ['href', 'title', 'target', 'rel'],
    ALLOW_DATA_ATTR: false,
    ALLOW_ARIA_ATTR: false,
  })
}
