import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { TarotPlusRoute, TarotPlusSessionStatus, type TarotPlusSession } from '@/types'

const getMock = vi.fn()
const getNextStepMock = vi.fn()
const addAnswerMock = vi.fn()
const generateReportMock = vi.fn()

vi.mock('@/api/tarotPlusApi', () => ({
  tarotPlusApi: {
    get: (...args: unknown[]) => getMock(...args),
    getNextStep: (...args: unknown[]) => getNextStepMock(...args),
    createPayment: vi.fn(),
    addAnswer: (...args: unknown[]) => addAnswerMock(...args),
    generateReport: (...args: unknown[]) => generateReportMock(...args),
    askFollowUp: vi.fn(),
  },
}))

import TarotPlusSessionView from '@/views/TarotPlusSessionView.vue'

describe('TarotPlusSessionView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    getMock.mockReset()
    getNextStepMock.mockReset()
    addAnswerMock.mockReset()
    generateReportMock.mockReset()
  })

  it('renders intake question', async () => {
    getMock.mockResolvedValue(sampleSession({ status: TarotPlusSessionStatus.Intake, intakeAnswerCount: 2 }))
    getNextStepMock.mockResolvedValue({
      status: TarotPlusSessionStatus.Intake,
      question: 'Что сейчас важно?',
      canGenerateReport: false,
      answerCount: 2,
      requiredAnswers: 5,
      maxAnswers: 9,
    })

    const wrapper = await mountView()

    expect(wrapper.find('[data-testid="tarot-plus-next-question"]').text()).toContain('Что сейчас важно?')
  })

  it('renders report markdown', async () => {
    getMock.mockResolvedValue(sampleSession({
      status: TarotPlusSessionStatus.ReportReady,
      reportMarkdown: '# Report title\n\nText',
    }))

    const wrapper = await mountView()

    expect(wrapper.find('[data-testid="tarot-plus-report-state"]').html()).toContain('<h1>Report title</h1>')
  })

  it('hides follow-up form when followUpsLeft is 0', async () => {
    getMock.mockResolvedValue(sampleSession({
      status: TarotPlusSessionStatus.Completed,
      reportMarkdown: '# Report',
      followUpsLeft: 0,
    }))

    const wrapper = await mountView()

    expect(wrapper.find('[data-testid="tarot-plus-follow-up-question"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="tarot-plus-follow-up-submit"]').exists()).toBe(false)
  })

  it('renders drawn spreads with replay control', async () => {
    getMock.mockResolvedValue(sampleSession({
      status: TarotPlusSessionStatus.ReportReady,
      reportMarkdown: '# Report',
      drawnSpreads: [
        {
          spreadId: 'current_core_7',
          spreadName: 'Текущее ядро',
          cards: [
            {
              position: 0,
              positionName: 'Где человек находится сейчас',
              cardId: 13,
              cardName: 'Смерть',
              imagePath: '/cards/death.jpg',
              isReversed: false,
              meaning: 'Трансформация',
            },
          ],
        },
      ],
    }))

    const wrapper = await mountView()

    expect(wrapper.find('[data-testid="tarot-plus-drawn-spreads"]').text()).toContain('Текущее ядро')
    expect(wrapper.find('[data-testid="tarot-plus-replay-spreads"]').exists()).toBe(true)
    expect(wrapper.find('.drawn-card').attributes('style')).toContain('--deal-index: 0')
  })

  it('shows card ritual while answer is being processed', async () => {
    getMock.mockResolvedValue(sampleSession({ status: TarotPlusSessionStatus.Intake, intakeAnswerCount: 2 }))
    getNextStepMock.mockResolvedValue({
      status: TarotPlusSessionStatus.Intake,
      question: 'Что сейчас важно?',
      canGenerateReport: false,
      answerCount: 2,
      requiredAnswers: 5,
      maxAnswers: 9,
    })

    let resolveAnswer: (value: TarotPlusSession) => void = () => {}
    addAnswerMock.mockReturnValue(new Promise<TarotPlusSession>((resolve) => {
      resolveAnswer = resolve
    }))

    const wrapper = await mountView()
    await wrapper.find('[data-testid="tarot-plus-answer"]').setValue('Нужно понять следующий шаг')
    await wrapper.find('[data-testid="tarot-plus-answer-submit"]').trigger('click')

    expect(wrapper.find('[data-testid="tarot-plus-ritual-loader"]').text()).toContain('Ответ принят')

    resolveAnswer(sampleSession({ status: TarotPlusSessionStatus.Intake, intakeAnswerCount: 3 }))
    await flushPromises()
  })
})

async function mountView() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/tarot-plus/:id', name: 'tarot-plus-session', component: TarotPlusSessionView },
    ],
  })
  router.push('/tarot-plus/tp1')
  await router.isReady()
  const wrapper = mount(TarotPlusSessionView, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

function sampleSession(overrides: Partial<TarotPlusSession> = {}): TarotPlusSession {
  return {
    id: 'tp1',
    status: TarotPlusSessionStatus.PreviewReady,
    route: TarotPlusRoute.GeneralLife,
    routeLabel: 'Жизненный обзор',
    coreRequest: 'Core',
    previewText: 'Preview',
    reportMarkdown: null,
    followUpsLeft: 2,
    priceRub: 100,
    paidAt: null,
    createdAt: '2026-05-16T00:00:00Z',
    updatedAt: '2026-05-16T00:00:00Z',
    expiresAt: '2026-06-16T00:00:00Z',
    answerCount: 0,
    intakeAnswerCount: 0,
    answers: [],
    drawnSpreads: [],
    followUps: [],
    ...overrides,
  }
}
