import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { TarotPlusRoute, TarotPlusSessionStatus, type TarotPlusSession } from '@/types'

const createPreviewMock = vi.fn()
const createPaymentMock = vi.fn()
const getMock = vi.fn()
const addAnswerMock = vi.fn()
const getNextStepMock = vi.fn()
const generateReportMock = vi.fn()
const askFollowUpMock = vi.fn()

vi.mock('@/api/tarotPlusApi', () => ({
  tarotPlusApi: {
    createPreview: (...args: unknown[]) => createPreviewMock(...args),
    createPayment: (...args: unknown[]) => createPaymentMock(...args),
    get: (...args: unknown[]) => getMock(...args),
    history: vi.fn(),
    addAnswer: (...args: unknown[]) => addAnswerMock(...args),
    getNextStep: (...args: unknown[]) => getNextStepMock(...args),
    generateReport: (...args: unknown[]) => generateReportMock(...args),
    askFollowUp: (...args: unknown[]) => askFollowUpMock(...args),
  },
}))

import { useTarotPlusStore } from '@/stores/useTarotPlusStore'

describe('useTarotPlusStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    createPreviewMock.mockReset()
    createPaymentMock.mockReset()
    getMock.mockReset()
    addAnswerMock.mockReset()
    getNextStepMock.mockReset()
    generateReportMock.mockReset()
    askFollowUpMock.mockReset()
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { assign: vi.fn() },
    })
  })

  it('createPreview stores current session', async () => {
    createPreviewMock.mockResolvedValue({
      session: sampleSession(),
      previewText: 'Preview',
      route: TarotPlusRoute.GeneralLife,
      routeLabel: 'Жизненный обзор',
    })

    const store = useTarotPlusStore()
    await store.createPreview({
      coreRequest: 'Core',
      mainSphere: 'Work',
      desiredOutcome: 'Plan',
    })

    expect(store.current?.id).toBe('tp1')
    expect(store.answers).toHaveLength(0)
    expect(store.error).toBeNull()
  })

  it('createPayment redirects to confirmationUrl', async () => {
    createPaymentMock.mockResolvedValue({
      paymentId: 'pay1',
      confirmationUrl: 'https://pay.example/confirm',
      status: 'pending',
    })
    const store = useTarotPlusStore()
    store.current = sampleSession()

    await store.createPayment()

    expect(createPaymentMock).toHaveBeenCalledWith('tp1')
    expect(window.location.assign).toHaveBeenCalledWith('https://pay.example/confirm')
  })

  it('generateReport stores returned report session', async () => {
    const ready = sampleSession({
      status: TarotPlusSessionStatus.ReportReady,
      reportMarkdown: '# Report',
    })
    generateReportMock.mockResolvedValue({
      session: ready,
      reportMarkdown: '# Report',
    })

    const store = useTarotPlusStore()
    await store.generateReport('tp1')

    expect(store.current?.status).toBe(TarotPlusSessionStatus.ReportReady)
    expect(store.current?.reportMarkdown).toBe('# Report')
  })

  it('load clears stale follow-up answer', async () => {
    getMock.mockResolvedValue(sampleSession())
    const store = useTarotPlusStore()
    store.lastFollowUpAnswer = 'Old answer'

    await store.load('tp1')

    expect(store.lastFollowUpAnswer).toBeNull()
  })

  it('askFollowUp stores last answer', async () => {
    askFollowUpMock.mockResolvedValue({
      session: sampleSession({ followUpsLeft: 1 }),
      answerMarkdown: 'Follow-up',
      followUpsLeft: 1,
    })

    const store = useTarotPlusStore()
    await store.askFollowUp('tp1', 'Что дальше?')

    expect(store.lastFollowUpAnswer).toBe('Follow-up')
    expect(store.current?.followUpsLeft).toBe(1)
  })
})

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
