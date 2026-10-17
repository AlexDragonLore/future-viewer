import { beforeEach, describe, expect, it, vi } from 'vitest'
import { adminApi } from '@/api/adminApi'
import { httpClient } from '@/api/httpClient'
vi.mock('@/api/httpClient', () => ({ httpClient: { get: vi.fn() } }))

describe('admin reading audience query', () => {
  beforeEach(() => { vi.mocked(httpClient.get).mockReset().mockResolvedValue({ data: { items: [], total: 0 } }) })
  it.each(['all', 'subscribers', 'registered', 'guests'] as const)('serializes %s with the existing search, paging and abort signal', async audience => {
    const controller = new AbortController()
    await adminApi.listReadings({ audience, search: 'вопрос', userId: 'user-id', page: 2, pageSize: 20 }, controller.signal)
    expect(httpClient.get).toHaveBeenCalledOnce()
    expect(httpClient.get).toHaveBeenCalledWith('/api/admin/readings', {
      signal: controller.signal,
      params: { audience, search: 'вопрос', userId: 'user-id', page: 2, pageSize: 20, includeInterpretation: false },
    })
  })
  it('defaults to all without changing the lightweight-list contract', async () => {
    await adminApi.listReadings()
    expect(httpClient.get).toHaveBeenCalledWith('/api/admin/readings', expect.objectContaining({
      params: expect.objectContaining({ audience: 'all', page: 1, includeInterpretation: false }),
    }))
  })
})
