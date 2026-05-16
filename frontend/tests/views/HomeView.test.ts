import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { SpreadType, SubscriptionStatusValue } from '@/types'

const validateQuestionMock = vi.fn()
vi.mock('@/api/readingApi', () => ({
  readingApi: {
    spreads: vi.fn(async () => [
      { type: SpreadType.SingleCard, name: 'One', cardCount: 1, positions: [] },
      { type: SpreadType.ThreeCard, name: 'Three', cardCount: 3, positions: [] },
      { type: SpreadType.CelticCross, name: 'Cross', cardCount: 10, positions: [] },
    ]),
    create: vi.fn(),
    validateQuestion: (...args: [unknown, unknown, unknown]) => validateQuestionMock(...args),
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

const personalizationMock = vi.fn()
const updatePersonalizationMock = vi.fn()
vi.mock('@/api/profileApi', () => ({
  profileApi: {
    personalization: (...args: []) => personalizationMock(...args),
    updatePersonalization: (...args: [unknown]) => updatePersonalizationMock(...args),
    deleteMemoryRule: vi.fn(),
    clearMemory: vi.fn(),
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
      { path: '/glossary', name: 'glossary', component: { template: '<div>glossary</div>' } },
      { path: '/tarot/decks/:slug', name: 'tarot-deck-seo', component: { template: '<div>deck</div>' } },
      { path: '/tarot/spreads/:slug', name: 'tarot-spread-seo', component: { template: '<div>spread</div>' } },
      { path: '/faq', name: 'faq', component: { template: '<div>faq</div>' } },
      { path: '/legal', name: 'legal', component: { template: '<div>legal</div>' } },
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
    personalizationMock.mockReset()
    updatePersonalizationMock.mockReset()
    validateQuestionMock.mockReset()
    validateQuestionMock.mockResolvedValue({
      status: 'accepted',
      reason: 'ok',
      suggestedQuestion: null,
    })
    statusMock.mockResolvedValue({
      status: SubscriptionStatusValue.None,
      expiresAt: null,
      isActive: false,
      freeReadingsUsedToday: 0,
      freeReadingsDailyLimit: 1,
      canCreateFreeReading: true,
    })
    personalizationMock.mockResolvedValue({
      firstName: 'Test',
      lastName: 'User',
      birthDate: '1990-01-01',
      isComplete: true,
      memoryRules: [],
    })
    updatePersonalizationMock.mockResolvedValue({
      firstName: 'Test',
      lastName: 'User',
      birthDate: '1990-01-01',
      isComplete: true,
      memoryRules: [],
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
    expect(validateQuestionMock).toHaveBeenCalled()
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

  it('asks authenticated user for personalization before first reading', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    personalizationMock.mockResolvedValue({
      firstName: null,
      lastName: null,
      birthDate: null,
      isComplete: false,
      memoryRules: [],
    })
    const { wrapper, router } = await mountHome()
    await wrapper.findAll('.spread-option')[0].trigger('click')
    await wrapper.find('textarea').setValue('Question?')
    expect(wrapper.find('[data-testid="personalization-intro"]').exists()).toBe(true)

    await wrapper.find('[data-testid="first-name-input"]').setValue('Ada')
    await wrapper.find('[data-testid="last-name-input"]').setValue('Lovelace')
    await wrapper.find('[data-testid="birth-date-input"]').setValue('1815-12-10')
    await wrapper.find('.glow-button').trigger('click')
    await flushPromises()

    expect(updatePersonalizationMock).toHaveBeenCalledWith({
      firstName: 'Ada',
      lastName: 'Lovelace',
      birthDate: '1815-12-10',
    })
    expect(validateQuestionMock).toHaveBeenCalled()
    expect(router.currentRoute.value.name).toBe('reading')
  })

  it('stays on home and shows validator response when question needs rewrite', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    validateQuestionMock.mockRejectedValue({
      response: {
        data: {
          error: 'question_needs_rewrite',
          message: 'Вопрос слишком общий.',
          suggestedQuestion: 'На что мне стоит обратить внимание?',
        },
      },
    })
    const { wrapper, router } = await mountHome()
    await wrapper.findAll('.spread-option')[0].trigger('click')
    await wrapper.find('textarea').setValue('что будет?')
    await wrapper.find('.glow-button').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('home')
    expect(sessionStorage.getItem('fv_pending')).toBeNull()
    expect(wrapper.find('[data-testid="question-validation"]').text()).toContain('Вопрос слишком общий.')
    expect(wrapper.find('[data-testid="apply-suggested-question"]').text()).toContain(
      'На что мне стоит обратить внимание?',
    )
  })

  it('adds a fallback suggestion when validator rejects without one', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    validateQuestionMock.mockRejectedValue({
      response: {
        data: {
          error: 'question_rejected',
          message: 'Вопрос не связан с реальной жизненной ситуацией.',
          suggestedQuestion: null,
        },
      },
    })
    const { wrapper } = await mountHome()
    await wrapper.findAll('.spread-option')[0].trigger('click')
    await wrapper.find('textarea').setValue('тест')
    await wrapper.find('.glow-button').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="question-validation"]').text()).toContain(
      'Вопрос не связан с реальной жизненной ситуацией.',
    )
    expect(wrapper.find('[data-testid="apply-suggested-question"]').text()).toContain(
      'Что мне важно понять про тему «тест»?',
    )
  })

  it('restores pending question and shows reading creation errors from reading screen', async () => {
    sessionStorage.setItem('fv_pending', JSON.stringify({
      spreadType: SpreadType.SingleCard,
      question: 'тест',
    }))
    sessionStorage.setItem('fv_reading_error', JSON.stringify({
      message: 'AI-провайдер не настроен.',
    }))

    const { wrapper } = await mountHome()

    expect((wrapper.find('textarea').element as HTMLTextAreaElement).value).toBe('тест')
    expect(wrapper.find('[data-testid="question-validation"]').text()).toContain('AI-провайдер не настроен.')
    expect(sessionStorage.getItem('fv_pending')).toBeNull()
    expect(sessionStorage.getItem('fv_reading_error')).toBeNull()
  })

  it('restores pending question and adds fallback suggestion for reading validation errors', async () => {
    sessionStorage.setItem('fv_pending', JSON.stringify({
      spreadType: SpreadType.SingleCard,
      question: 'тест',
    }))
    sessionStorage.setItem('fv_question_validation', JSON.stringify({
      message: 'Вопрос не связан с реальной жизненной ситуацией.',
      suggestedQuestion: null,
    }))

    const { wrapper } = await mountHome()

    expect((wrapper.find('textarea').element as HTMLTextAreaElement).value).toBe('тест')
    expect(wrapper.find('[data-testid="question-validation"]').text()).toContain(
      'Вопрос не связан с реальной жизненной ситуацией.',
    )
    expect(wrapper.find('[data-testid="apply-suggested-question"]').text()).toContain(
      'Что мне важно понять про тему «тест»?',
    )
    expect(sessionStorage.getItem('fv_pending')).toBeNull()
    expect(sessionStorage.getItem('fv_question_validation')).toBeNull()
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
    expect(wrapper.text()).toContain('Доступ активен')
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

  it('renders current deck blurb with link to the SEO deck page', async () => {
    const { wrapper } = await mountHome()
    const blurb = wrapper.find('[data-testid="home-deck-blurb"]')
    expect(blurb.exists()).toBe(true)
    expect(blurb.text()).toContain('Rider–Waite–Smith')
    expect(blurb.find('a').attributes('href')).toBe('/tarot/decks/rider-waite-smith')
  })

  it('renders spread blurb that updates when spread changes', async () => {
    const { wrapper } = await mountHome()
    const blurb = wrapper.find('[data-testid="home-spread-blurb"]')
    expect(blurb.exists()).toBe(true)
    expect(blurb.find('a').attributes('href')).toBe('/tarot/spreads/tri-karty')
    const buttons = wrapper.findAll('.spread-option')
    await buttons[0].trigger('click')
    expect(wrapper.find('[data-testid="home-spread-blurb"] a').attributes('href')).toBe('/tarot/spreads/karta-dnya')
  })
})
