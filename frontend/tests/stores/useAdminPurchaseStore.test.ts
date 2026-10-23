import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { clearAccountSession } from '@/utils/accountSession'
import { adminPurchaseFixtures } from '../fixtures/adminPurchases'

const list = vi.fn()
vi.mock('@/api/adminApi', () => ({ adminApi: { listPurchases: (...args: unknown[]) => list(...args) } }))
import { useAdminPurchaseStore } from '@/stores/useAdminPurchaseStore'

describe('admin purchase history', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    list.mockReset()
  })

  it('requests paid purchases by default and resets pagination only when filters change', async () => {
    const store = useAdminPurchaseStore()
    list.mockResolvedValue({ items: [adminPurchaseFixtures[0]], total: 25 })
    await store.load()
    expect(list).toHaveBeenLastCalledWith({ search: '', status: 'paid', userId: null, page: 1, pageSize: 20 }, expect.any(AbortSignal))
    store.page = 2
    store.setFilters({ search: ' person@example.com ', status: 'all', userId: 'buyer' })
    expect(store.page).toBe(1)
    await store.load()
    expect(list).toHaveBeenLastCalledWith({ search: 'person@example.com', status: 'all', userId: 'buyer', page: 1, pageSize: 20 }, expect.any(AbortSignal))
    store.page = 2
    store.setFilters({ search: 'person@example.com', status: 'all', userId: 'buyer' })
    expect(store.page).toBe(2)
  })

  it('ignores a stale response after switching filters', async () => {
    let finish!: (value: unknown) => void
    list.mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
    const store = useAdminPurchaseStore()
    const first = store.load()
    const signal = list.mock.calls[0][1] as AbortSignal
    store.setFilters({ search: '', status: 'refunded', userId: null })
    list.mockResolvedValueOnce({ items: [adminPurchaseFixtures[21]], total: 1 })
    await store.load()
    expect(signal.aborted).toBe(true)
    finish({ items: adminPurchaseFixtures, total: 26 })
    await first
    expect(store.items).toEqual([adminPurchaseFixtures[21]])
    expect(store.total).toBe(1)
    expect(store.loading).toBe(false)
  })

  it('clears purchase data and pending results on logout', async () => {
    let finish!: (value: unknown) => void
    const store = useAdminPurchaseStore()
    list.mockResolvedValueOnce({ items: adminPurchaseFixtures, total: 26 })
    await store.load()
    list.mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
    const pending = store.load()
    clearAccountSession()
    finish({ items: adminPurchaseFixtures, total: 26 })
    await pending
    expect(store.items).toEqual([])
    expect(store.total).toBe(0)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('clears previous results on failure and supports retry', async () => {
    const store = useAdminPurchaseStore()
    list.mockResolvedValueOnce({ items: adminPurchaseFixtures, total: 26 })
    await store.load()
    list.mockRejectedValueOnce({ response: { data: { message: 'Ошибка загрузки' } } })
    await store.load()
    expect(store.error).toBe('Ошибка загрузки')
    expect(store.items).toEqual([])
    expect(store.total).toBe(0)
    list.mockResolvedValueOnce({ items: [], total: 0 })
    await store.load()
    expect(store.error).toBeNull()
    expect(store.loading).toBe(false)
  })
})
