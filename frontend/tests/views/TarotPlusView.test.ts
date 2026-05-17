import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { TarotPlusRoute, TarotPlusSessionStatus, type TarotPlusSession } from '@/types'

const createPreviewMock = vi.fn()
const createPaymentMock = vi.fn()

vi.mock('@/api/tarotPlusApi', () => ({
  tarotPlusApi: {
    createPreview: (...args: unknown[]) => createPreviewMock(...args),
    createPayment: (...args: unknown[]) => createPaymentMock(...args),
  },
}))

import TarotPlusView from '@/views/TarotPlusView.vue'

describe('TarotPlusView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    createPreviewMock.mockReset()
    createPaymentMock.mockReset()
  })

  it('renders price 100 ₽', async () => {
    const wrapper = await mountView()

    expect(wrapper.find('[data-testid="tarot-plus-price"]').text()).toContain('100 ₽')
  })

  it('creates preview', async () => {
    createPreviewMock.mockResolvedValue({
      session: sampleSession(),
      previewText: 'Preview',
      route: TarotPlusRoute.GeneralLife,
      routeLabel: 'Жизненный обзор',
    })
    const wrapper = await mountView()

    await wrapper.find('[data-testid="tarot-plus-core"]').setValue('Хочу понять следующий шаг')
    await wrapper.find('[data-testid="tarot-plus-sphere"]').setValue('работа')
    await wrapper.find('[data-testid="tarot-plus-outcome"]').setValue('план действий')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(createPreviewMock).toHaveBeenCalled()
    expect(wrapper.find('[data-testid="tarot-plus-preview-result"]').text()).toContain('Preview')
  })

  it('redirects to payment confirmationUrl', async () => {
    createPreviewMock.mockResolvedValue({
      session: sampleSession(),
      previewText: 'Preview',
      route: TarotPlusRoute.GeneralLife,
      routeLabel: 'Жизненный обзор',
    })
    createPaymentMock.mockResolvedValue({
      paymentId: 'pay1',
      confirmationUrl: 'https://pay.example/confirm',
      status: 'pending',
    })
    const wrapper = await mountView()
    await wrapper.find('[data-testid="tarot-plus-core"]').setValue('Хочу понять следующий шаг')
    await wrapper.find('[data-testid="tarot-plus-sphere"]').setValue('работа')
    await wrapper.find('[data-testid="tarot-plus-outcome"]').setValue('план действий')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    await wrapper.find('[data-testid="tarot-plus-pay"]').trigger('click')
    await flushPromises()

    expect(createPaymentMock).toHaveBeenCalledWith('tp1')
  })
})

async function mountView() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/tarot-plus', name: 'tarot-plus', component: TarotPlusView },
      { path: '/tarot-plus/:id', name: 'tarot-plus-session', component: { template: '<div />' } },
    ],
  })
  router.push('/tarot-plus')
  await router.isReady()
  const wrapper = mount(TarotPlusView, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

function sampleSession(): TarotPlusSession {
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
  }
}
