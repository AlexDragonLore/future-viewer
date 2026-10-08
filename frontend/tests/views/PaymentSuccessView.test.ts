import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { saveGuestContinuation } from '@/utils/guestReading'

const statusMock = vi.fn()
vi.mock('@/api/subscriptionApi', () => ({ subscriptionApi: { status: () => statusMock() } }))

import PaymentSuccessView from '@/views/PaymentSuccessView.vue'

async function mountPaymentSuccess() {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div>home</div>' } },
      { path: '/result', component: { template: '<div>result</div>' } },
      { path: '/payment/success', component: PaymentSuccessView },
    ],
  })
  await router.push('/payment/success')
  const wrapper = mount(PaymentSuccessView, { global: { plugins: [router] } })
  await flushPromises()
  return { wrapper, router }
}

describe('PaymentSuccessView guest continuation', () => {
  beforeEach(() => {
    localStorage.clear()
    localStorage.setItem('fv_token', 'verified-session')
    statusMock.mockResolvedValue({ isActive: true })
  })

  it('returns to the saved guest reading after paid access activates', async () => {
    saveGuestContinuation({ ticket: 'guest-ticket', expiresAt: new Date(Date.now() + 86_400_000).toISOString() })
    const { wrapper, router } = await mountPaymentSuccess()
    expect(wrapper.get('button').text()).toBe('Продолжить мой расклад')
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/result')
  })

  it('returns to the home when there is no saved guest reading', async () => {
    const { wrapper, router } = await mountPaymentSuccess()
    expect(wrapper.get('button').text()).toBe('К раскладам')
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/')
  })

  it('keeps an unconfirmed payment on the normal home route even with a preview ticket', async () => {
    statusMock.mockResolvedValue({ isActive: false })
    saveGuestContinuation({ ticket: 'guest-ticket', expiresAt: new Date(Date.now() + 86_400_000).toISOString() })
    const { wrapper, router } = await mountPaymentSuccess()
    expect(wrapper.get('button').text()).toBe('На главную')
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/')
  })
})
