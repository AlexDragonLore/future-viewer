import { describe, it, expect } from 'vitest'
import { extractApiError } from '@/api/httpClient'

describe('extractApiError', () => {
  it('returns details joined when present', () => {
    const err = { response: { data: { error: 'validation_error', details: ['a', 'b'] } } }
    expect(extractApiError(err)).toBe('a; b')
  })

  it('returns message when no details', () => {
    const err = { response: { data: { message: 'nope' } } }
    expect(extractApiError(err)).toBe('nope')
  })

  it('returns error field when no message', () => {
    const err = { response: { data: { error: 'conflict' } } }
    expect(extractApiError(err)).toBe('conflict')
  })

  it('falls back to axios message', () => {
    const err = { message: 'Network Error' }
    expect(extractApiError(err)).toBe('Network Error')
  })

  it('uses default fallback for unknown shape', () => {
    expect(extractApiError({}, 'fallback')).toBe('fallback')
  })
})
