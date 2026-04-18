import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import { DeckType, SpreadType, type Reading } from '@/types'
import { useReadingStore } from '@/stores/useReadingStore'

import ResultView from '@/views/ResultView.vue'

const sample: Reading = {
  id: 'r1',
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

async function mountResult(withReading: Reading | null): Promise<{ wrapper: ReturnType<typeof mount>; router: Router }> {
  setActivePinia(createPinia())
  const store = useReadingStore()
  store.current = withReading
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: { template: '<div>home</div>' } },
      { path: '/result', name: 'result', component: ResultView },
    ],
  })
  router.push('/result')
  await router.isReady()
  const wrapper = mount(ResultView, { global: { plugins: [router] } })
  return { wrapper, router }
}

describe('ResultView', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })
  afterEach(() => {
    vi.useRealTimers()
  })

  it('redirects to home when no reading is present', async () => {
    const { router } = await mountResult(null)
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('home')
  })

  it('renders reading header and question', async () => {
    const { wrapper } = await mountResult(sample)
    await flushPromises()
    expect(wrapper.text()).toContain('THREE CARD')
    expect(wrapper.text()).toContain('where to?')
  })

  it('typewriter reveals interpretation over time', async () => {
    const { wrapper } = await mountResult(sample)
    await flushPromises()
    expect(wrapper.text()).not.toContain('The stars align.')
    await vi.advanceTimersByTimeAsync(18 * sample.interpretation!.length + 20)
    await flushPromises()
    expect(wrapper.text()).toContain('The stars align.')
  })

  it('clicking "новый расклад" resets store and navigates home', async () => {
    const { wrapper, router } = await mountResult(sample)
    await flushPromises()
    await wrapper.find('.glow-button').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('home')
  })
})
