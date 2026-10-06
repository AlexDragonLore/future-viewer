import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import { DeckType, SpreadType, SubscriptionStatusValue, type Reading } from '@/types'
import { useReadingStore } from '@/stores/useReadingStore'
import { useAuthStore } from '@/stores/useAuthStore'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'

const createPayment = vi.fn()
vi.mock('@/api/paymentApi', () => ({ paymentApi: { createAccessPayment: (acceptance: unknown) => createPayment(acceptance) } }))
vi.mock('@/content/legal', () => ({
  legalDocumentVersions: { offer: 'offer-current' },
  loadLegalDocuments: vi.fn().mockResolvedValue(undefined),
  isApprovedProcessorUrl: () => false,
}))

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

async function mountResult(withReading: Reading | null, setup?: () => void): Promise<{ wrapper: ReturnType<typeof mount>; router: Router }> {
  setActivePinia(createPinia())
  const store = useReadingStore()
  store.current = withReading
  setup?.()
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: { template: '<div>home</div>' } },
      { path: '/result', name: 'result', component: ResultView },
      { path: '/auth', name: 'auth', component: { template: '<div>auth</div>' } },
      { path: '/legal/:document', component: { template: '<div>legal</div>' } },
    ],
  })
  router.push('/result')
  await router.isReady()
  const wrapper = mount(ResultView, { global: { plugins: [router] } })
  return { wrapper, router }
}

function enableFreeAccountCheckout() {
  const auth = useAuthStore()
  auth.token = 'qa-session'
  auth.userId = 'qa-user'
  auth.subscription = {
    status: SubscriptionStatusValue.None,
    expiresAt: null,
    isActive: false,
    freeReadingsUsedToday: 1,
    freeReadingsDailyLimit: 1,
    canCreateFreeReading: false,
  }
  const config = usePublicConfigStore()
  config.paymentsEnabled = true
  config.paymentProducts = [{ tariffCode: 'pro-45d', amount: 640, currency: 'RUB', accessDays: 45 }]
}

async function finishTyping() {
  await vi.advanceTimersByTimeAsync(5000)
  await flushPromises()
}

describe('ResultView', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    localStorage.clear()
    createPayment.mockReset().mockResolvedValue({ paymentId: 'qa-order', confirmationUrl: '', status: 'pending' })
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
    expect(wrapper.find('[data-testid="ai-disclaimer"]').exists()).toBe(true)
  })

  it('sanitizes executable HTML in the streamed AI interpretation', async () => {
    const { wrapper } = await mountResult({
      ...sample,
      interpretation: '<img src=x onerror="alert(1)"> [bad](javascript:alert(1))',
    })
    await vi.advanceTimersByTimeAsync(5000)
    await flushPromises()
    const rendered = wrapper.get('.prose-mystic').html()
    expect(rendered).not.toContain('<img')
    expect(rendered).not.toContain('onerror')
    expect(rendered).not.toContain('javascript:')
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

  it('ends a guest preview with a registration link back to this result', async () => {
    const { wrapper } = await mountResult({ ...sample, isPreview: true, interpretation: 'Первая половина…' })
    await vi.advanceTimersByTimeAsync(5000)
    await flushPromises()
    expect(wrapper.get('[data-testid="guest-unlock"]').text()).toContain('полное толкование этой карты')
    expect(wrapper.get('a.guest-register').attributes('href')).toBe('/auth?mode=register&redirect=/result')
    expect(wrapper.text()).not.toContain('begin')
  })

  it('shows the whole completed guest preview and registration CTA before animation frames run', async () => {
    const interpretation = 'Готовое гостевое толкование. '.repeat(40) + 'Последняя строка доступной половины…'
    const { wrapper } = await mountResult({ ...sample, isPreview: true, interpretation }, () => {
      const store = useReadingStore()
      store.cardsReady = true
      store.streamingDone = true
      store.streamingText = interpretation
    })

    expect(wrapper.get('.prose-mystic').text()).toBe(interpretation)
    expect(wrapper.find('.caret').exists()).toBe(false)
    expect(wrapper.get('[data-testid="guest-unlock"]').isVisible()).toBe(true)
    expect(wrapper.get('a.guest-register').attributes('href')).toBe('/auth?mode=register&redirect=/result')
    expect(wrapper.find('[data-testid="result-paid-offer"]').exists()).toBe(false)
  })

  it('offers the current server product only after the full text is visible and uses the existing consent-aware checkout', async () => {
    const { wrapper } = await mountResult(sample, enableFreeAccountCheckout)
    expect(wrapper.find('[data-testid="result-paid-offer"]').exists()).toBe(false)
    await finishTyping()
    const offer = wrapper.get('[data-testid="result-paid-offer"]')
    expect(offer.text()).toContain('640')
    expect(offer.text()).toContain('45 дней')
    expect(offer.text()).toContain('Все 3 расклада без лимита')
    expect(offer.get('button').attributes('disabled')).toBeDefined()
    expect(createPayment).not.toHaveBeenCalled()
    await offer.get('[data-testid="payment-offer-acceptance"]').setValue(true)
    await offer.get('button').trigger('click')
    await flushPromises()
    expect(createPayment).toHaveBeenCalledOnce()
    expect(createPayment).toHaveBeenCalledWith({ offerAccepted: true, offerVersion: 'offer-current', tariffCode: 'pro-45d' })
  })

  it.each(['guest', 'preview', 'subscriber', 'disabled', 'missing-product', 'unknown-subscription', 'refreshing-subscription', 'loading'])
  ('does not offer payment for %s', async (reason) => {
    const { wrapper } = await mountResult({ ...sample, isPreview: reason === 'preview' }, () => {
      enableFreeAccountCheckout()
      const auth = useAuthStore()
      const config = usePublicConfigStore()
      if (reason === 'guest') auth.token = null
      if (reason === 'subscriber') auth.subscription!.isActive = true
      if (reason === 'disabled') config.paymentsEnabled = false
      if (reason === 'missing-product') config.paymentProducts = []
      if (reason === 'unknown-subscription') auth.subscription = null
      if (reason === 'refreshing-subscription') auth.subscriptionLoading = true
      if (reason === 'loading') useReadingStore().loading = true
    })
    await finishTyping()
    expect(wrapper.find('[data-testid="result-paid-offer"]').exists()).toBe(false)
    expect(createPayment).not.toHaveBeenCalled()
  })

  it('waits for stream completion even when all currently received text has been displayed', async () => {
    const { wrapper } = await mountResult(sample, () => {
      enableFreeAccountCheckout()
      const store = useReadingStore()
      store.cardsReady = true
      store.streamingDone = false
      store.streamingText = sample.interpretation!
    })
    await finishTyping()
    expect(wrapper.find('[data-testid="result-paid-offer"]').exists()).toBe(false)
    useReadingStore().streamingDone = true
    await flushPromises()
    expect(wrapper.find('[data-testid="result-paid-offer"]').exists()).toBe(true)
  })
})
