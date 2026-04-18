import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import { DeckType, SpreadType, type Reading } from '@/types'

const getMock = vi.fn()

vi.mock('@/api/readingApi', () => ({
  readingApi: {
    get: (id: string) => getMock(id),
    spreads: vi.fn(),
    create: vi.fn(),
    history: vi.fn(),
  },
}))

import ReadingDetailView from '@/views/ReadingDetailView.vue'

const sample: Reading = {
  id: 'abc-123',
  spreadType: SpreadType.ThreeCard,
  spreadName: 'Three card',
  question: 'where to?',
  createdAt: '2026-04-14T12:00:00Z',
  cards: [
    {
      position: 0,
      positionName: 'Past',
      positionMeaning: 'origin',
      cardId: 1,
      cardName: 'The Fool',
      imagePath: '',
      isReversed: false,
      meaning: 'begin',
    },
  ],
  interpretation: 'The stars align.',
  deckType: DeckType.RWS,
}

async function mountDetail(id: string): Promise<{ wrapper: ReturnType<typeof mount>; router: Router }> {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/history', name: 'history', component: { template: '<div>h</div>' } },
      { path: '/reading/:id', name: 'reading-detail', component: ReadingDetailView },
    ],
  })
  router.push(`/reading/${id}`)
  await router.isReady()
  const wrapper = mount(ReadingDetailView, { global: { plugins: [router] } })
  return { wrapper, router }
}

describe('ReadingDetailView', () => {
  beforeEach(() => {
    getMock.mockReset()
  })

  it('shows loading state before the request resolves', async () => {
    getMock.mockImplementation(() => new Promise(() => {}))
    const { wrapper } = await mountDetail('abc-123')
    expect(wrapper.text()).toContain('загружаю')
  })

  it('renders the loaded reading', async () => {
    getMock.mockResolvedValue(sample)
    const { wrapper } = await mountDetail('abc-123')
    await flushPromises()
    expect(getMock).toHaveBeenCalledWith('abc-123')
    expect(wrapper.text()).toContain('THREE CARD')
    expect(wrapper.text()).toContain('where to?')
    expect(wrapper.text()).toContain('The stars align.')
    expect(wrapper.text()).toContain('Past')
  })

  it('shows an error when the request fails', async () => {
    getMock.mockRejectedValue({ response: { data: { message: 'Not found' } } })
    const { wrapper } = await mountDetail('missing')
    await flushPromises()
    expect(wrapper.text()).toContain('Not found')
  })
})
