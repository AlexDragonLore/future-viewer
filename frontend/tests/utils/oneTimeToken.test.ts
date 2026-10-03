import { describe, expect, it } from 'vitest'
import { extractOneTimeToken } from '@/utils/oneTimeToken'

describe('extractOneTimeToken', () => {
  it('prefers a fragment token so it is not sent to the server', () => {
    expect(extractOneTimeToken('#token=fragment%20value', 'legacy')).toBe('fragment value')
  })

  it('accepts legacy query links only for already-issued messages', () => {
    expect(extractOneTimeToken('', 'legacy-token')).toBe('legacy-token')
  })
})
