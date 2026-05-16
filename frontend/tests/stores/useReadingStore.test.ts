import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { DeckType, SpreadType, type Reading } from '@/types'

const spreadsMock = vi.fn()
const createMock = vi.fn()
const createStreamMock = vi.fn()

vi.mock('@/api/readingApi', () => ({
  readingApi: {
    spreads: () => spreadsMock(),
    create: (type: SpreadType, question: string, deckType: DeckType) =>
      createMock(type, question, deckType),
    createStream: (...args: unknown[]) => createStreamMock(...args),
    get: vi.fn(),
    history: vi.fn(),
  },
}))

import { useReadingStore } from '@/stores/useReadingStore'
import { useDeckStore } from '@/stores/useDeckStore'

const sampleReading: Reading = {
  id: 'r1',
  spreadType: SpreadType.ThreeCard,
  spreadName: 'Three card',
  question: 'what?',
  createdAt: '2026-04-14T12:00:00Z',
  cards: [],
  interpretation: 'Stub interpretation',
  deckType: DeckType.RWS,
}

describe('useReadingStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    spreadsMock.mockReset()
    createMock.mockReset()
    createStreamMock.mockReset()
    localStorage.clear()
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

  it('create passes selected deck from deck store', async () => {
    createMock.mockResolvedValue(sampleReading)
    const deck = useDeckStore()
    deck.select(DeckType.Thoth)
    const store = useReadingStore()
    await store.create(SpreadType.ThreeCard, 'what?')
    expect(createMock).toHaveBeenCalledWith(SpreadType.ThreeCard, 'what?', DeckType.Thoth)
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

  it('createStream stops streaming when interpretation fails after cards arrive', async () => {
    createStreamMock.mockImplementation(async (_type, _question, _deckType, handlers) => {
      handlers.onCards(sampleReading)
      handlers.onChunk('Partial interpretation')
      throw new Error('stream broke')
    })

    const store = useReadingStore()
    const { cardsPromise, donePromise } = store.createStream(SpreadType.SingleCard, 'q')

    await expect(cardsPromise).resolves.toEqual(sampleReading)
    await expect(donePromise).rejects.toThrow('stream broke')
    expect(store.streamingDone).toBe(true)
    expect(store.loading).toBe(false)
    expect(store.error).toBe('stream broke')
    expect(store.current?.interpretation).toContain('Partial interpretation')
    expect(store.current?.interpretation).toContain('stream broke')
  })

  it('createStream forwards abort signal to the streaming api', async () => {
    const controller = new AbortController()
    createStreamMock.mockImplementation(async (_type, _question, _deckType, handlers, signal) => {
      handlers.onCards(sampleReading)
      handlers.onDone()
      expect(signal).toBe(controller.signal)
    })

    const store = useReadingStore()
    const { donePromise } = store.createStream(SpreadType.SingleCard, 'q', controller.signal)

    await expect(donePromise).resolves.toBeUndefined()
    expect(createStreamMock).toHaveBeenCalledTimes(1)
  })

  it('createStream does not surface raw internal_error from failed responses', async () => {
    createStreamMock.mockRejectedValue(Object.assign(new Error('internal_error'), {
      response: { data: { error: 'internal_error' } },
    }))

    const store = useReadingStore()
    const { cardsPromise, donePromise } = store.createStream(SpreadType.SingleCard, 'q')

    await expect(cardsPromise).rejects.toThrow('internal_error')
    await expect(donePromise).rejects.toThrow('internal_error')
    expect(store.error).toBe('Не удалось создать расклад')
    expect(store.streamingText).toBe('Не удалось создать расклад')
  })
})
