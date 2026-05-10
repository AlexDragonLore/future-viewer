import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'

const forgotPasswordMock = vi.fn()

vi.mock('@/api/authApi', () => ({
  authApi: {
    forgotPassword: (...args: [string]) => forgotPasswordMock(...args),
  },
}))

import ForgotPasswordView from '@/views/ForgotPasswordView.vue'

async function mountView(): Promise<{ wrapper: ReturnType<typeof mount>; router: Router }> {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div>home</div>' } },
      { path: '/auth', name: 'auth', component: { template: '<div>auth</div>' } },
      { path: '/forgot-password', name: 'forgot-password', component: ForgotPasswordView },
    ],
  })
  router.push('/forgot-password')
  await router.isReady()
  const wrapper = mount(ForgotPasswordView, { global: { plugins: [router] } })
  return { wrapper, router }
}

describe('ForgotPasswordView', () => {
  beforeEach(() => {
    localStorage.clear()
    forgotPasswordMock.mockReset()
  })

  it('shows the form initially', async () => {
    const { wrapper } = await mountView()
    expect(wrapper.text()).toContain('Забыли пароль')
    expect(wrapper.find('input[type="email"]').exists()).toBe(true)
  })

  it('calls forgotPassword on submit and shows success message', async () => {
    forgotPasswordMock.mockResolvedValue(undefined)
    const { wrapper } = await mountView()
    await wrapper.find('input[type="email"]').setValue('me@example.com')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(forgotPasswordMock).toHaveBeenCalledWith('me@example.com')
    expect(wrapper.text()).toContain('отправили письмо')
  })

  it('shows error when API fails', async () => {
    forgotPasswordMock.mockRejectedValue({
      response: { data: { error: 'validation_error', details: ['bad'] } },
      message: 'fail',
    })
    const { wrapper } = await mountView()
    await wrapper.find('input[type="email"]').setValue('me@example.com')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('bad')
  })
})
