import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { SpreadType, SubscriptionStatusValue } from '@/types'

vi.mock('@/api/readingApi', () => ({
  readingApi: {
    spreads: vi.fn(async () => [
      { type: SpreadType.SingleCard, name: 'One', cardCount: 1, positions: [] },
      { type: SpreadType.ThreeCard, name: 'Three', cardCount: 3, positions: [] },
      { type: SpreadType.CelticCross, name: 'Cross', cardCount: 10, positions: [] },
    ]),
    create: vi.fn(),
    get: vi.fn(),
    history: vi.fn(),
  },
}))

const statusMock = vi.fn()
vi.mock('@/api/subscriptionApi', () => ({
  subscriptionApi: {
    status: (...args: []) => statusMock(...args),
  },
}))

import HomeView from '@/views/HomeView.vue'

async function mountHome() {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: HomeView },
      { path: '/reading', name: 'reading', component: { template: '<div>reading</div>' } },
      { path: '/auth', name: 'auth', component: { template: '<div>auth</div>' } },
      { path: '/history', name: 'history', component: { template: '<div>hist</div>' } },
    ],
  })
  router.push('/')
  await router.isReady()
  const wrapper = mount(HomeView, { global: { plugins: [router] } })
  await flushPromises()
  return { wrapper, router }
}

describe('HomeView', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
    statusMock.mockReset()
    statusMock.mockResolvedValue({
      status: SubscriptionStatusValue.None,
      expiresAt: null,
      isActive: false,
      freeReadingsUsedToday: 0,
      freeReadingsDailyLimit: 1,
      canCreateFreeReading: true,
    })
  })

  it('start button is disabled when question is empty', async () => {
    const { wrapper } = await mountHome()
    const startBtn = wrapper.find('.glow-button')
    expect((startBtn.element as HTMLButtonElement).disabled).toBe(true)
  })

  it('start button enables after typing a question', async () => {
    const { wrapper } = await mountHome()
    await wrapper.find('textarea').setValue('What awaits me?')
    const startBtn = wrapper.find('.glow-button')
    expect((startBtn.element as HTMLButtonElement).disabled).toBe(false)
  })

  it('begin stores payload in sessionStorage and navigates to reading when authenticated', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    const { wrapper, router } = await mountHome()
    const buttons = wrapper.findAll('.spread-option')
    await buttons[0].trigger('click')
    await wrapper.find('textarea').setValue('Question?')
    await wrapper.find('.glow-button').trigger('click')
    await flushPromises()

    const stored = sessionStorage.getItem('fv_pending')
    expect(stored).not.toBeNull()
    const parsed = JSON.parse(stored!)
    expect(parsed.question).toBe('Question?')
    expect(parsed.spreadType).toBe(SpreadType.SingleCard)
    expect(router.currentRoute.value.name).toBe('reading')
  })

  it('begin redirects unauthenticated user to auth while saving payload', async () => {
    const { wrapper, router } = await mountHome()
    await wrapper.find('textarea').setValue('Question?')
    await wrapper.find('.glow-button').trigger('click')
    await flushPromises()

    expect(sessionStorage.getItem('fv_pending')).not.toBeNull()
    expect(router.currentRoute.value.name).toBe('auth')
  })

  it('shows subscription badge with remaining free quota when authenticated', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    const { wrapper } = await mountHome()
    expect(wrapper.text()).toContain('Бесплатно сегодня: 1/1')
  })

  it('shows active subscription badge for subscriber', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    statusMock.mockResolvedValue({
      status: SubscriptionStatusValue.Active,
      expiresAt: '2030-01-01T00:00:00Z',
      isActive: true,
      freeReadingsUsedToday: 0,
      freeReadingsDailyLimit: 1,
      canCreateFreeReading: true,
    })
    const { wrapper } = await mountHome()
    expect(wrapper.text()).toContain('Подписка активна')
  })

  it('blocks free authenticated user from multi-card spread', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    const { wrapper } = await mountHome()
    await wrapper.find('textarea').setValue('Any question?')
    expect(wrapper.find('[data-testid="block-warning"]').exists()).toBe(true)
    const btn = wrapper.find('.glow-button').element as HTMLButtonElement
    expect(btn.disabled).toBe(true)
  })

  it('blocks free authenticated user when daily quota exhausted', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    statusMock.mockResolvedValue({
      status: SubscriptionStatusValue.None,
      expiresAt: null,
      isActive: false,
      freeReadingsUsedToday: 1,
      freeReadingsDailyLimit: 1,
      canCreateFreeReading: false,
    })
    const { wrapper } = await mountHome()
    await wrapper.findAll('.spread-option')[0].trigger('click')
    await wrapper.find('textarea').setValue('Any question?')
    expect(wrapper.find('[data-testid="block-warning"]').exists()).toBe(true)
    const btn = wrapper.find('.glow-button').element as HTMLButtonElement
    expect(btn.disabled).toBe(true)
  })

  it('selecting a spread updates active state', async () => {
    const { wrapper } = await mountHome()
    const buttons = wrapper.findAll('.spread-option')
    expect(buttons.length).toBeGreaterThanOrEqual(3)
    await buttons[0].trigger('click')
    expect(buttons[0].classes()).toContain('active')
  })

  it('renders current deck blurb with link to glossary anchor', async () => {
    const { wrapper } = await mountHome()
    const blurb = wrapper.find('[data-testid="home-deck-blurb"]')
    expect(blurb.exists()).toBe(true)
    expect(blurb.text()).toContain('Rider–Waite–Smith')
    expect(blurb.find('a').attributes('href')).toContain('#deck-rws')
  })

  it('renders spread blurb that updates when spread changes', async () => {
    const { wrapper } = await mountHome()
    const blurb = wrapper.find('[data-testid="home-spread-blurb"]')
    expect(blurb.exists()).toBe(true)
    expect(blurb.find('a').attributes('href')).toContain('#spread-three-card')
    const buttons = wrapper.findAll('.spread-option')
    await buttons[0].trigger('click')
    expect(wrapper.find('[data-testid="home-spread-blurb"] a').attributes('href')).toContain(
      '#spread-single-card',
    )
  })
})
