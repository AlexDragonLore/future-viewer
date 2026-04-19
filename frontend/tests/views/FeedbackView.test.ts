import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import { FeedbackStatus, type FeedbackInfo } from '@/types'

const getByTokenMock = vi.fn()
const submitMock = vi.fn()

vi.mock('@/api/feedbackApi', () => ({
  feedbackApi: {
    getByToken: (...args: [string]) => getByTokenMock(...args),
    submit: (...args: [string, string]) => submitMock(...args),
    getMy: vi.fn(),
  },
}))

import FeedbackView from '@/views/FeedbackView.vue'

function pendingFeedback(): FeedbackInfo {
  return {
    id: 'fb1',
    readingId: 'r1',
    question: 'что мне делать?',
    interpretation: 'Звёзды советуют **действовать**.',
    aiScore: null,
    aiScoreReason: null,
    isSincere: null,
    selfReport: null,
    status: FeedbackStatus.Pending,
    createdAt: '2026-04-18T10:00:00Z',
    answeredAt: null,
  }
}

function scoredFeedback(): FeedbackInfo {
  return {
    id: 'fb2',
    readingId: 'r2',
    question: 'стоит ли переезжать?',
    interpretation: 'Карты указывают на перемены.',
    aiScore: 8,
    aiScoreReason: 'Видна попытка следовать совету',
    isSincere: true,
    selfReport: 'Попробовал всё обдумать за неделю.',
    status: FeedbackStatus.Scored,
    createdAt: '2026-04-10T10:00:00Z',
    answeredAt: '2026-04-11T10:00:00Z',
  }
}

async function mountFeedback(token: string): Promise<{ wrapper: ReturnType<typeof mount>; router: Router }> {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/feedback/:token', name: 'feedback', component: FeedbackView },
    ],
  })
  router.push(`/feedback/${token}`)
  await router.isReady()
  const wrapper = mount(FeedbackView, { global: { plugins: [router] } })
  return { wrapper, router }
}

describe('FeedbackView', () => {
  beforeEach(() => {
    getByTokenMock.mockReset()
    submitMock.mockReset()
  })

  it('shows the loading state before the request resolves', async () => {
    getByTokenMock.mockImplementation(() => new Promise(() => {}))
    const { wrapper } = await mountFeedback('tok1')
    expect(wrapper.find('[data-testid="feedback-loading"]').exists()).toBe(true)
  })

  it('renders the question and interpretation and form for a pending feedback', async () => {
    getByTokenMock.mockResolvedValue(pendingFeedback())
    const { wrapper } = await mountFeedback('tok1')
    await flushPromises()
    expect(getByTokenMock).toHaveBeenCalledWith('tok1')
    expect(wrapper.find('[data-testid="feedback-question"]').text()).toContain('что мне делать?')
    expect(wrapper.find('[data-testid="feedback-interpretation"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-result"]').exists()).toBe(false)
  })

  it('shows result without the form when feedback is already scored', async () => {
    getByTokenMock.mockResolvedValue(scoredFeedback())
    const { wrapper } = await mountFeedback('tok2')
    await flushPromises()
    expect(wrapper.find('[data-testid="feedback-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="feedback-result"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('8 / 10')
    expect(wrapper.find('[data-testid="feedback-reason"]').text()).toContain('попытка')
    expect(wrapper.find('[data-testid="feedback-self-report"]').text()).toContain('Попробовал')
  })

  it('shows an error when the initial request fails', async () => {
    getByTokenMock.mockRejectedValue({ response: { data: { message: 'Ссылка истекла' } } })
    const { wrapper } = await mountFeedback('bad')
    await flushPromises()
    expect(wrapper.find('[data-testid="feedback-load-error"]').text()).toContain('истекла')
  })

  it('submits the answer and shows the score on success', async () => {
    getByTokenMock.mockResolvedValue(pendingFeedback())
    submitMock.mockResolvedValue({
      ...pendingFeedback(),
      aiScore: 9,
      aiScoreReason: 'молодец',
      isSincere: true,
      selfReport: 'Я старался следовать совету целую неделю',
      status: FeedbackStatus.Scored,
      answeredAt: '2026-04-19T10:00:00Z',
    })

    const { wrapper } = await mountFeedback('tok-ok')
    await flushPromises()

    await wrapper.get('[data-testid="feedback-textarea"]').setValue('Я старался следовать совету целую неделю')
    await wrapper.get('[data-testid="feedback-form"]').trigger('submit')
    await flushPromises()

    expect(submitMock).toHaveBeenCalledWith('tok-ok', 'Я старался следовать совету целую неделю')
    expect(wrapper.find('[data-testid="feedback-result"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('9 / 10')
    expect(wrapper.find('[data-testid="feedback-reason"]').text()).toContain('молодец')
  })

  it('marks an insincere submission with the dedicated tag', async () => {
    getByTokenMock.mockResolvedValue(pendingFeedback())
    submitMock.mockResolvedValue({
      ...pendingFeedback(),
      aiScore: 1,
      aiScoreReason: 'неискренне',
      isSincere: false,
      selfReport: 'ответ на десять символов минимум',
      status: FeedbackStatus.Scored,
      answeredAt: '2026-04-19T10:00:00Z',
    })

    const { wrapper } = await mountFeedback('tok-bad')
    await flushPromises()

    await wrapper.get('[data-testid="feedback-textarea"]').setValue('ответ на десять символов минимум')
    await wrapper.get('[data-testid="feedback-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('не искренне')
    expect(wrapper.text()).toContain('1 / 10')
  })

  it('shows a submit error without destroying the form', async () => {
    getByTokenMock.mockResolvedValue(pendingFeedback())
    submitMock.mockRejectedValue({ response: { data: { message: 'уже отвечено' } } })

    const { wrapper } = await mountFeedback('tok')
    await flushPromises()

    await wrapper.get('[data-testid="feedback-textarea"]').setValue('ответ достаточной длины для отправки')
    await wrapper.get('[data-testid="feedback-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="feedback-error"]').text()).toContain('уже отвечено')
    expect(wrapper.find('[data-testid="feedback-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-result"]').exists()).toBe(false)
  })
})
