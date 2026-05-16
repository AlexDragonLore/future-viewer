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
    vi.restoreAllMocks()
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

  it('keeps following streamed interpretation while new text is typed', async () => {
    const scrollIntoView = vi.fn()
    Object.defineProperty(Element.prototype, 'scrollIntoView', {
      configurable: true,
      value: scrollIntoView,
    })
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
    Object.defineProperty(document.documentElement, 'scrollHeight', { configurable: true, value: 2400 })
    Object.defineProperty(document.body, 'scrollHeight', { configurable: true, value: 2400 })
    Object.defineProperty(window, 'innerHeight', { configurable: true, value: 700 })
    Object.defineProperty(window, 'scrollY', { configurable: true, value: 0 })

    await mountResult({ ...sample, interpretation: null })
    const store = useReadingStore()
    store.current = { ...sample, interpretation: null }
    store.cardsReady = true
    store.streamingDone = false
    store.streamingText = 'Первая строка интерпретации. Вторая строка появляется ниже.'
    await flushPromises()
    await vi.advanceTimersByTimeAsync(1200)

    expect(scrollIntoView).toHaveBeenCalledWith({ block: 'end', inline: 'nearest', behavior: 'auto' })
    expect(scrollTo).not.toHaveBeenCalled()
  })

  it('does not force-scroll when the user has scrolled away from the stream tail', async () => {
    const scrollIntoView = vi.fn()
    Object.defineProperty(Element.prototype, 'scrollIntoView', {
      configurable: true,
      value: scrollIntoView,
    })
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
    Object.defineProperty(document.documentElement, 'scrollHeight', { configurable: true, value: 4200 })
    Object.defineProperty(document.body, 'scrollHeight', { configurable: true, value: 4200 })
    Object.defineProperty(window, 'innerHeight', { configurable: true, value: 700 })
    Object.defineProperty(window, 'scrollY', { configurable: true, value: 0 })

    await mountResult({ ...sample, interpretation: null })
    await flushPromises()
    await vi.advanceTimersByTimeAsync(80)
    scrollIntoView.mockClear()
    scrollTo.mockClear()
    window.dispatchEvent(new WheelEvent('wheel'))
    window.dispatchEvent(new Event('scroll'))

    const store = useReadingStore()
    store.current = { ...sample, interpretation: null }
    store.cardsReady = true
    store.streamingDone = false
    store.streamingText = 'Новый фрагмент интерпретации.'
    await flushPromises()
    await vi.advanceTimersByTimeAsync(80)

    expect(scrollIntoView).not.toHaveBeenCalled()
    expect(scrollTo).not.toHaveBeenCalled()
  })

  it('clicking "новый расклад" resets store and navigates home', async () => {
    const { wrapper, router } = await mountResult(sample)
    await flushPromises()
    await wrapper.find('.glow-button').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('home')
  })
})
