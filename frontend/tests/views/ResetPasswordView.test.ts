import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'

const resetPasswordMock = vi.fn()

vi.mock('@/api/authApi', () => ({
  authApi: {
    resetPassword: (...args: [string, string]) => resetPasswordMock(...args),
  },
}))

vi.mock('@/api/subscriptionApi', () => ({
  subscriptionApi: {
    status: vi.fn(async () => ({ isActive: false, canCreateFreeReading: true })),
  },
}))

import ResetPasswordView from '@/views/ResetPasswordView.vue'

async function mountView(url = '/reset-password?token=abc123'): Promise<{ wrapper: ReturnType<typeof mount>; router: Router }> {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div>home</div>' } },
      { path: '/forgot-password', component: { template: '<div>fp</div>' } },
      { path: '/reset-password', name: 'reset-password', component: ResetPasswordView },
    ],
  })
  router.push(url)
  await router.isReady()
  const wrapper = mount(ResetPasswordView, { global: { plugins: [router] } })
  return { wrapper, router }
}

describe('ResetPasswordView', () => {
  beforeEach(() => {
    localStorage.clear()
    resetPasswordMock.mockReset()
  })

  it('shows error when token is missing', async () => {
    const { wrapper } = await mountView('/reset-password')
    expect(wrapper.text()).toContain('недействительна')
  })

  it('rejects mismatched passwords', async () => {
    const { wrapper } = await mountView()
    const passwords = wrapper.findAll('input[type="password"]')
    await passwords[0].setValue('newpass12')
    await passwords[1].setValue('different12')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(resetPasswordMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('не совпадают')
  })

  it('submits new password and redirects home', async () => {
    resetPasswordMock.mockResolvedValue({
      accessToken: 't',
      expiresAt: '',
      userId: 'u',
      email: 'u@x.com',
      isAdmin: false,
    })
    const { wrapper, router } = await mountView()
    const passwords = wrapper.findAll('input[type="password"]')
    await passwords[0].setValue('newpass12')
    await passwords[1].setValue('newpass12')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(resetPasswordMock).toHaveBeenCalledWith('abc123', 'newpass12')
    expect(router.currentRoute.value.path).toBe('/')
  })

  it('shows API error when reset fails', async () => {
    resetPasswordMock.mockRejectedValue({
      response: { data: { error: 'unauthorized', details: ['expired'] } },
      message: 'fail',
    })
    const { wrapper } = await mountView()
    const passwords = wrapper.findAll('input[type="password"]')
    await passwords[0].setValue('newpass12')
    await passwords[1].setValue('newpass12')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('expired')
  })
})
