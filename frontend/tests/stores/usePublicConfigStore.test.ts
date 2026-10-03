import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

const getConfig = vi.fn()

vi.mock('@/api/publicApi', () => ({
  publicApi: {
    getConfig: (...args: unknown[]) => getConfig(...args),
  },
}))

import { usePublicConfigStore } from '@/stores/usePublicConfigStore'

describe('usePublicConfigStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    getConfig.mockReset()
  })

  it('loads supportEmail from the public config endpoint', async () => {
    getConfig.mockResolvedValueOnce({ supportEmail: 'hello@example.com' })
    const store = usePublicConfigStore()
    await store.load()
    expect(store.supportEmail).toBe('hello@example.com')
    expect(store.loaded).toBe(true)
  })

  it('does not re-fetch after first load', async () => {
    getConfig.mockResolvedValue({ supportEmail: 'a@b.c' })
    const store = usePublicConfigStore()
    await store.load()
    await store.load()
    expect(getConfig).toHaveBeenCalledTimes(1)
  })

  it('falls back to empty string when the endpoint fails', async () => {
    getConfig.mockRejectedValueOnce(new Error('network'))
    const store = usePublicConfigStore()
    await store.load()
    expect(store.supportEmail).toBe('')
    expect(store.loaded).toBe(true)
  })
})

describe('payments capability', () => {
  it.each([undefined, null, false, 'true'])('stays disabled unless the backend returns boolean true (%s)', async (value) => {
    setActivePinia(createPinia())
    getConfig.mockResolvedValueOnce({ supportEmail: '', paymentsEnabled: value })
    const store = usePublicConfigStore()
    await store.load()
    expect(store.paymentsEnabled).toBe(false)
  })

  it('accepts explicit payment enablement from the backend', async () => {
    setActivePinia(createPinia())
    getConfig.mockResolvedValueOnce({ supportEmail: '', paymentsEnabled: true })
    const store = usePublicConfigStore()
    await store.load()
    expect(store.paymentsEnabled).toBe(true)
  })
})
