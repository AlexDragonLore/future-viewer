import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { DeckType, SpreadType, type Reading, type ReadingCard } from '@/types'
import { useReadingStore } from '@/stores/useReadingStore'

const createStreamMock = vi.fn()
vi.mock('@/api/readingApi', () => ({
  readingApi: {
    createStream: (...args: unknown[]) => createStreamMock(...args),
  },
}))

vi.mock('@/utils/preloadImages', () => ({
  preloadImages: vi.fn(async () => undefined),
}))

vi.mock('@/composables/useAudio', () => ({
  playShuffle: vi.fn(),
  playCardFlip: vi.fn(),
  playReveal: vi.fn(),
  unlockAudio: vi.fn(),
}))

import ReadingView from '@/views/ReadingView.vue'

function buildCard(position: number): ReadingCard {
  return {
    position,
    positionName: `Position ${position}`,
    positionMeaning: 'meaning',
    cardId: position + 1,
    cardName: `Card ${position}`,
    imagePath: `/cards/${position}.jpg`,
    isReversed: false,
    meaning: 'card meaning',
  }
}

function buildReading(cardCount: number): Reading {
  return {
    id: 'reading-1',
    spreadType: SpreadType.CelticCross,
    spreadName: 'Celtic Cross',
    question: 'Question?',
    createdAt: '2026-05-15T12:00:00Z',
    cards: Array.from({ length: cardCount }, (_, i) => buildCard(i)),
    interpretation: 'Interpretation',
    deckType: DeckType.RWS,
  }
}

async function mountReading() {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: { template: '<div>home</div>' } },
      { path: '/reading', name: 'reading', component: ReadingView },
      { path: '/result', name: 'result', component: { template: '<div>result</div>' } },
    ],
  })
  router.push('/reading')
  await router.isReady()
  const wrapper = mount(ReadingView, { global: { plugins: [router] } })
  await flushPromises()
  return { wrapper, router, store: useReadingStore() }
}

describe('ReadingView', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    localStorage.clear()
    sessionStorage.clear()
    sessionStorage.setItem('fv_pending', JSON.stringify({
      spreadType: SpreadType.SingleCard,
      question: 'Question?',
    }))
    createStreamMock.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('keeps a single-card spread to one card when the stream returns extra cards', async () => {
    const oversizedReading = buildReading(10)
    createStreamMock.mockImplementation(async (_spreadType, _question, _deckType, handlers) => {
      handlers.onCards(oversizedReading)
      handlers.onDone()
    })

    const { router, store } = await mountReading()

    await flushPromises()
    await vi.runAllTimersAsync()
    await flushPromises()

    expect(store.current?.spreadType).toBe(SpreadType.SingleCard)
    expect(store.current?.cards).toHaveLength(1)
    expect(router.currentRoute.value.name).toBe('result')
  })

  it('returns to home with a stored error when reading creation fails before cards arrive', async () => {
    createStreamMock.mockRejectedValue(Object.assign(new Error('AI-провайдер не настроен.'), {
      response: { data: { message: 'AI-провайдер не настроен.' } },
    }))

    const { router } = await mountReading()

    await flushPromises()
    await vi.runAllTimersAsync()
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('home')
    expect(sessionStorage.getItem('fv_reading_error')).toContain('AI-провайдер не настроен.')
  })

  it('uses a fixed viewport board without document-height layout shifts', async () => {
    createStreamMock.mockImplementation(async (_spreadType, _question, _deckType, handlers) => {
      handlers.onCards(buildReading(1))
      handlers.onDone()
    })

    const { wrapper } = await mountReading()
    const board = wrapper.find('.reading-board')

    expect(board.exists()).toBe(true)
    expect(board.classes()).not.toContain('min-h-screen')
  })
})
