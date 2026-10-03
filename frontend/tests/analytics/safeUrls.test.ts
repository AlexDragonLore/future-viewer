import { describe, expect, it } from 'vitest'
import { sanitizePageUrl, sanitizeReferrer } from '@/analytics/safeUrls'

const origin = 'https://alex-taro.ru'

describe('analytics URL minimization', () => {
  it('retains technical ad attribution only on the initial landing', () => {
    const raw = '/?utm_source=yandex&utm_medium=cpc&utm_campaign=123&utm_content=ad_9&yclid=123456789012'
      + '&utm_term=личный+вопрос&email=private%40example.com&token=secret#question'
    expect(sanitizePageUrl(raw, origin, true)).toBe(
      `${origin}/?utm_source=yandex&utm_medium=cpc&utm_campaign=123&utm_content=ad_9&yclid=123456789012`,
    )
    expect(sanitizePageUrl(raw, origin)).toBe(`${origin}/`)
  })

  it('rejects contact details and arbitrary text disguised as campaign labels', () => {
    expect(sanitizePageUrl('/?utm_source=private%40example.com&utm_campaign=+7+(900)+1234567'
      + '&utm_content=Как+поступить&yclid=private@example.com', origin, true)).toBe(`${origin}/`)
  })

  it.each([
    ['/verify-email?token=private', '/verify-email'],
    ['/reset-password?token=private#private', '/reset-password'],
    ['/reading/69b20a98-68cf-472c-a4ce-cb4c27abbc59?question=private', '/reading/detail'],
    ['/feedback/private-token', '/feedback'],
    ['/admin/users/private-id', '/admin/users/detail'],
    ['/tarot/cards/private-content', '/tarot/cards'],
    ['/unknown/private-email@example.com', '/other'],
  ])('maps %s to a fixed route without private values', (raw, expected) => {
    expect(sanitizePageUrl(raw, origin)).toBe(`${origin}${expected}`)
  })

  it('reduces referrers to their host and uses a nonempty safe fallback', () => {
    expect(sanitizeReferrer('https://mail.example.com/u/private?token=secret', origin))
      .toBe('https://mail.example.com/')
    expect(sanitizeReferrer('', origin)).toBe(`${origin}/`)
    expect(sanitizeReferrer('https://user:secret@example.com/private', origin)).toBe(`${origin}/`)
    expect(sanitizeReferrer('javascript:alert(1)', origin)).toBe(`${origin}/`)
  })
})
