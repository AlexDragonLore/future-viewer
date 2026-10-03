import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { SpreadType, SubscriptionStatusValue } from '@/types'

const validateQuestionMock = vi.fn()
const statusMock = vi.fn()
const privacySettingsMock = vi.fn()

vi.mock('@/api/readingApi', () => ({
  readingApi: {
    validateQuestion: (...args: [unknown, unknown, unknown]) => validateQuestionMock(...args),
    spreads: vi.fn(),
    create: vi.fn(),
    createStream: vi.fn(),
    get: vi.fn(),
    history: vi.fn(),
  },
}))

vi.mock('@/api/subscriptionApi', () => ({
  subscriptionApi: { status: () => statusMock() },
}))

vi.mock('@/api/privacyApi', () => ({
  privacyApi: {
    settings: () => privacySettingsMock(),
    consents: vi.fn(async () => []),
    accountDeletionStatus: vi.fn(async () => null),
  },
}))

import HomeView from '@/views/HomeView.vue'
import { useReadingStore } from '@/stores/useReadingStore'

async function mountHome(setup?: () => void) {
  setActivePinia(createPinia())
  setup?.()
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: HomeView },
      { path: '/reading', name: 'reading', component: { template: '<div>reading</div>' } },
      { path: '/auth', name: 'auth', component: { template: '<div>auth</div>' } },
      { path: '/history', name: 'history', component: { template: '<div>history</div>' } },
      { path: '/faq', name: 'faq', component: { template: '<div>faq</div>' } },
      { path: '/tarot/decks/:slug', name: 'tarot-deck-seo', component: { template: '<div>deck</div>' } },
      { path: '/tarot/spreads/:slug', name: 'tarot-spread-seo', component: { template: '<div>spread</div>' } },
      { path: '/legal/:document', component: { template: '<div>legal</div>' } },
    ],
  })
  router.push('/')
  await router.isReady()
  const wrapper = mount(HomeView, { global: { plugins: [router] } })
  await flushPromises()
  return { wrapper, router, store: useReadingStore() }
}

function authenticate() {
  localStorage.setItem('fv_token', 'test-token')
  localStorage.setItem('fv_email', 'u@x.test')
}

async function selectSingleCardAndType(wrapper: ReturnType<typeof mount>, question: string) {
  await wrapper.findAll('.spread-option')[0].trigger('click')
  await wrapper.find('textarea').setValue(question)
}

describe('HomeView privacy and question safety', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    validateQuestionMock.mockReset()
    statusMock.mockReset()
    privacySettingsMock.mockReset()
    validateQuestionMock.mockResolvedValue({
      status: 'accepted',
      reason: 'ok',
      suggestedQuestion: null,
      message: 'ok',
      canContinue: true,
      requiresSubscription: false,
    })
    statusMock.mockResolvedValue({
      status: SubscriptionStatusValue.None,
      expiresAt: null,
      isActive: false,
      freeReadingsUsedToday: 0,
      freeReadingsDailyLimit: 1,
      canCreateFreeReading: true,
    })
    privacySettingsMock.mockResolvedValue({ historyEnabled: false })
  })

  it('keeps history saving off by default and shows the exact disclaimer', async () => {
    authenticate()
    const { wrapper } = await mountHome()
    expect((wrapper.get('[data-testid="save-to-history"]').element as HTMLInputElement).checked).toBe(false)
    expect(wrapper.get('[data-testid="ai-disclaimer"]').text()).toContain(
      'Результат не является достоверным предсказанием, медицинской, юридической, психологической или финансовой консультацией',
    )
  })

  it('does not require or collect profile fields before a reading', async () => {
    authenticate()
    const { wrapper } = await mountHome()
    expect(wrapper.find('[data-testid="personalization-intro"]').exists()).toBe(false)
    expect(wrapper.find('input[type="date"]').exists()).toBe(false)
  })

  it('passes an accepted question through in-memory state without sessionStorage', async () => {
    authenticate()
    const { wrapper, router, store } = await mountHome()
    await selectSingleCardAndType(wrapper, 'На что обратить внимание в проекте?')
    await wrapper.get('button.glow-button').trigger('click')
    await flushPromises()

    expect(validateQuestionMock).toHaveBeenCalled()
    expect(store.pending).toMatchObject({
      question: 'На что обратить внимание в проекте?',
      spreadType: SpreadType.SingleCard,
      validated: true,
      saveToHistory: false,
    })
    expect(sessionStorage.getItem('fv_pending')).toBeNull()
    expect(router.currentRoute.value.name).toBe('reading')
  })

  it('starts a guest single-card reading without authentication or validation round trip', async () => {
    const { wrapper, router, store } = await mountHome()
    await wrapper.find('textarea').setValue('Как посмотреть на новую задачу?')
    await wrapper.get('button.glow-button').trigger('click')
    await flushPromises()

    expect(store.pending).toMatchObject({ question: 'Как посмотреть на новую задачу?', validated: true, spreadType: SpreadType.SingleCard, saveToHistory: false })
    expect(sessionStorage.length).toBe(0)
    expect(router.currentRoute.value.name).toBe('reading')
    expect(validateQuestionMock).not.toHaveBeenCalled()
  })

  it('lets a guest start with one click and a default question', async () => {
    const { wrapper, router, store } = await mountHome()
    expect(wrapper.find('.spread-option').exists()).toBe(false)
    expect(wrapper.find('[data-testid="save-to-history"]').exists()).toBe(false)
    expect(wrapper.get('button.glow-button').attributes('disabled')).toBeUndefined()
    await wrapper.get('button.glow-button').trigger('click')
    await flushPromises()
    expect(store.pending?.question).toBe('На что мне сейчас стоит обратить внимание?')
    expect(router.currentRoute.value.name).toBe('reading')
  })

  it.each([
    ['email', 'Напиши ответ для user@host.ru'],
    ['phone', 'Что будет с человеком +7 999 123-45-67?'],
    ['diagnosis', 'Болен ли я раком?'],
  ])('blocks a question containing %s before any API request', async (_caseName, text) => {
    authenticate()
    const { wrapper, router, store } = await mountHome()
    await selectSingleCardAndType(wrapper, text)
    await wrapper.get('button.glow-button').trigger('click')
    await flushPromises()

    expect(validateQuestionMock).not.toHaveBeenCalled()
    expect(store.pending).toBeNull()
    expect(router.currentRoute.value.name).toBe('home')
    expect(wrapper.get('[data-testid="question-validation"]').text()).toBeTruthy()
  })

  it('never lets a subscriber override a rejected response', async () => {
    authenticate()
    statusMock.mockResolvedValue({
      status: SubscriptionStatusValue.Active,
      expiresAt: '2030-01-01T00:00:00Z',
      isActive: true,
      freeReadingsUsedToday: 0,
      freeReadingsDailyLimit: 1,
      canCreateFreeReading: true,
    })
    validateQuestionMock.mockResolvedValue({
      status: 'rejected',
      reason: 'sensitive',
      suggestedQuestion: 'Как обезличить вопрос?',
      message: 'Исходный вопрос нельзя отправлять.',
      canContinue: true,
      requiresSubscription: false,
    })

    const { wrapper, router, store } = await mountHome()
    await selectSingleCardAndType(wrapper, 'Расскажи про другого человека')
    await wrapper.get('button.glow-button').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('home')
    expect(store.pending).toBeNull()
    expect(wrapper.get('[data-testid="question-validation"]').text()).toContain('нельзя отправлять')
    expect(wrapper.find('[data-testid="continue-with-warning"]').exists()).toBe(false)
  })

  it('passes the explicit per-reading history choice to pending state', async () => {
    authenticate()
    const { wrapper, store } = await mountHome()
    await selectSingleCardAndType(wrapper, 'Какой аспект задачи рассмотреть?')
    await wrapper.get('[data-testid="save-to-history"]').setValue(true)
    await wrapper.get('button.glow-button').trigger('click')
    await flushPromises()
    expect(store.pending?.saveToHistory).toBe(true)
  })

  it('uses the account history preference as the initial authenticated choice', async () => {
    authenticate()
    privacySettingsMock.mockResolvedValue({ historyEnabled: true })
    const { wrapper } = await mountHome()
    expect((wrapper.get('[data-testid="save-to-history"]').element as HTMLInputElement).checked).toBe(true)
  })

  it('shows a content-free workflow error without restoring a raw question from storage', async () => {
    const { wrapper } = await mountHome(() => {
      useReadingStore().setWorkflowIssue({ message: 'AI-интеграция временно отключена.' })
    })
    expect(wrapper.get('[data-testid="question-validation"]').text()).toContain('временно отключена')
    expect((wrapper.get('textarea').element as HTMLTextAreaElement).value).toBe('')
    expect(sessionStorage.length).toBe(0)
  })

  it('still enforces the free product quota independently from question safety', async () => {
    authenticate()
    const { wrapper } = await mountHome()
    await wrapper.findAll('.spread-option')[1].trigger('click')
    await wrapper.find('textarea').setValue('Какой аспект задачи рассмотреть?')
    expect(wrapper.find('[data-testid="block-warning"]').exists()).toBe(true)
    expect((wrapper.get('button.glow-button').element as HTMLButtonElement).disabled).toBe(true)
  })
})
