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

  it('does not expose raw internal_error to users', () => {
    const err = { response: { data: { error: 'internal_error' } } }
    expect(extractApiError(err, 'Не удалось создать расклад')).toBe('Не удалось создать расклад')
  })

  it('falls back to axios message', () => {
    const err = { message: 'Network Error' }
    expect(extractApiError(err)).toBe('Network Error')
  })

  it('uses default fallback for unknown shape', () => {
    expect(extractApiError({}, 'fallback')).toBe('fallback')
  })
})

describe('HTTP session boundary', () => {
  it('discards a prior account response after logout or account change', async () => {
    const { httpClient } = await import('@/api/httpClient')
    const { clearAccountSession } = await import('@/utils/accountSession')
    let finish!: () => void
    let dispatched!: () => void
    const started = new Promise<void>((resolve) => { dispatched = resolve })
    const response = httpClient.get('/api/privacy/settings', {
      adapter: (config) => new Promise((resolve) => {
        finish = () => resolve({ data: { historyEnabled: true }, status: 200, statusText: 'OK', headers: {}, config })
        dispatched()
      }),
    })
    const assertion = expect(response).rejects.toMatchObject({ code: 'ERR_CANCELED' })
    await started
    clearAccountSession()
    finish()
    await assertion
  })
})
