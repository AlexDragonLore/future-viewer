import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { SpreadType, type Reading } from '@/types'

const spreadsMock = vi.fn()
const createMock = vi.fn()

vi.mock('@/api/readingApi', () => ({
  readingApi: {
    spreads: () => spreadsMock(),
    create: (type: SpreadType, question: string) => createMock(type, question),
    get: vi.fn(),
    history: vi.fn(),
  },
}))

import { useReadingStore } from '@/stores/useReadingStore'

const sampleReading: Reading = {
  id: 'r1',
  spreadType: SpreadType.ThreeCard,
  spreadName: 'Three card',
  question: 'what?',
  createdAt: '2026-04-14T12:00:00Z',
  cards: [],
  interpretation: 'Stub interpretation',
}

describe('useReadingStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    spreadsMock.mockReset()
    createMock.mockReset()
  })

  it('loadSpreads caches after first call', async () => {
    spreadsMock.mockResolvedValue([
      { type: SpreadType.SingleCard, name: 'One', cardCount: 1, positions: [] },
    ])
    const store = useReadingStore()
    await store.loadSpreads()
    await store.loadSpreads()
    expect(spreadsMock).toHaveBeenCalledTimes(1)
    expect(store.spreads).toHaveLength(1)
  })

  it('create sets current reading on success', async () => {
    createMock.mockResolvedValue(sampleReading)
    const store = useReadingStore()
    await store.create(SpreadType.ThreeCard, 'what?')
    expect(store.current).toEqual(sampleReading)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('create surfaces validation error details from backend', async () => {
    createMock.mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 400,
        data: { error: 'validation_error', details: ['Question required', 'Spread invalid'] },
      },
      message: 'Request failed',
    })
    const store = useReadingStore()
    await expect(store.create(SpreadType.SingleCard, '')).rejects.toBeDefined()
    expect(store.error).toBe('Question required; Spread invalid')
    expect(store.loading).toBe(false)
    expect(store.current).toBeNull()
  })

  it('reset clears current and error', async () => {
    createMock.mockResolvedValue(sampleReading)
    const store = useReadingStore()
    await store.create(SpreadType.ThreeCard, 'q')
    store.reset()
    expect(store.current).toBeNull()
    expect(store.error).toBeNull()
  })
})
