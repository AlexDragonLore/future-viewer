import { describe, expect, it } from 'vitest'
import { safeMarkdown } from '@/utils/safeMarkdown'

describe('safeMarkdown', () => {
  it('escapes raw HTML and removes executable attributes and protocols', () => {
    const html = safeMarkdown([
      '<img src=x onerror="window.hacked=true">',
      '<script>window.hacked=true</script>',
      '[bad](javascript:alert(1))',
      '[good](https://safe.invalid/path)',
    ].join('\n\n'))

    expect(html).not.toContain('<img')
    expect(html).not.toContain('<script')
    expect(html).not.toContain('onerror')
    expect(html).not.toContain('javascript:')
    expect(html).toContain('https://safe.invalid/path')
    expect(html).toContain('rel="noopener noreferrer nofollow"')
  })

  it('keeps only the supported presentational Markdown subset', () => {
    const html = safeMarkdown('## Заголовок\n\n**Важно**\n\n- один\n- два')
    expect(html).toContain('Заголовок')
    expect(html).toContain('<strong>Важно</strong>')
    expect(html).toContain('<ul>')
  })
})
