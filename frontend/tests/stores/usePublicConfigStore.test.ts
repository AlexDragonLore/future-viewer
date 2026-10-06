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
    getConfig.mockResolvedValueOnce({ supportEmail: '', paymentsEnabled: true,
      paymentProducts: [
        { tariffCode: 'pro-30d', amount: 299, currency: 'RUB', accessDays: 30 },
        { tariffCode: 'pro-7d', amount: 99, currency: 'RUB', accessDays: 7 },
      ],
    })
    const store = usePublicConfigStore()
    await store.load()
    expect(store.paymentsEnabled).toBe(true)
    expect(store.paidProducts.map(product => product.tariffCode)).toEqual(['pro-7d', 'pro-30d'])
    expect(store.paidProducts[0].price).toContain('99')
    expect(store.paidProducts[1].price).toContain('299')
  })

  it('supports the legacy single-product response during an API update', async () => {
    setActivePinia(createPinia())
    getConfig.mockResolvedValueOnce({ paymentsEnabled: true,
      paymentProduct: { amount: 300, currency: 'RUB', accessDays: 30 },
    })
    const store = usePublicConfigStore()
    await store.load()
    expect(store.paymentProducts).toEqual([{ tariffCode: 'pro-30d', amount: 300, currency: 'RUB', accessDays: 30 }])
    expect(store.paymentsEnabled).toBe(true)
  })

  it('disables checkout when tariff data is missing or invalid', async () => {
    setActivePinia(createPinia())
    getConfig.mockResolvedValueOnce({ paymentsEnabled: true,
      paymentProducts: [
        { tariffCode: 'pro-7d', amount: -99, currency: 'RUB', accessDays: 7 },
        { tariffCode: '', amount: 299, currency: 'RUB', accessDays: 30 },
        { tariffCode: 'pro-30d', amount: 299, currency: 'USD', accessDays: 30 },
      ],
    })
    const store = usePublicConfigStore()
    await store.load()
    expect(store.paymentProducts).toEqual([])
    expect(store.paymentsEnabled).toBe(false)
  })
})
