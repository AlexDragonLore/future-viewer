import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'

const loginMock = vi.fn()
const registerMock = vi.fn()
const statusMock = vi.fn()

vi.mock('@/api/authApi', () => ({
  authApi: {
    login: (...args: [string, string]) => loginMock(...args),
    register: (...args: [string, string]) => registerMock(...args),
  },
}))

vi.mock('@/api/subscriptionApi', () => ({
  subscriptionApi: {
    status: (...args: []) => statusMock(...args),
  },
}))

import AuthView from '@/views/AuthView.vue'

async function mountAuth(initialPath = '/auth'): Promise<{ wrapper: ReturnType<typeof mount>; router: Router }> {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div>home</div>' } },
      { path: '/auth', name: 'auth', component: AuthView },
      { path: '/history', component: { template: '<div>h</div>' } },
      { path: '/forgot-password', name: 'forgot-password', component: { template: '<div>fp</div>' } },
    ],
  })
  router.push(initialPath)
  await router.isReady()
  const wrapper = mount(AuthView, { global: { plugins: [router] } })
  return { wrapper, router }
}

describe('AuthView', () => {
  beforeEach(() => {
    localStorage.clear()
    loginMock.mockReset()
    registerMock.mockReset()
    statusMock.mockReset()
    statusMock.mockResolvedValue({
      status: 0,
      expiresAt: null,
      isActive: false,
      freeReadingsUsedToday: 0,
      freeReadingsDailyLimit: 1,
      canCreateFreeReading: true,
    })
  })

  it('starts in login mode', async () => {
    const { wrapper } = await mountAuth()
    expect(wrapper.text()).toContain('Войти')
  })

  it('toggles to register mode', async () => {
    const { wrapper } = await mountAuth()
    await wrapper.find('button.auth-secondary-action').trigger('click')
    expect(wrapper.text()).toContain('Регистрация')
    expect(wrapper.text()).toContain('Создать')
  })

  it('calls login and redirects home on success', async () => {
    loginMock.mockResolvedValue({ accessToken: 't', email: 'u@x.com', expiresAt: '', userId: 'u' })
    const { wrapper, router } = await mountAuth()
    await wrapper.find('input[type="email"]').setValue('u@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(loginMock).toHaveBeenCalledWith('u@x.com', 'password1')
    expect(router.currentRoute.value.path).toBe('/')
  })

  it('honors redirect query parameter', async () => {
    loginMock.mockResolvedValue({ accessToken: 't', email: 'u@x.com', expiresAt: '', userId: 'u' })
    const { wrapper, router } = await mountAuth('/auth?redirect=/history')
    await wrapper.find('input[type="email"]').setValue('u@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/history')
  })

  it('shows backend validation details on failure', async () => {
    loginMock.mockRejectedValue({
      response: { data: { error: 'validation_error', details: ['Bad email', 'Bad pw'] } },
      message: 'Request failed',
    })
    const { wrapper } = await mountAuth()
    await wrapper.find('input[type="email"]').setValue('u@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(wrapper.text()).toContain('Bad email; Bad pw')
  })

  it('localizes invalid credential errors', async () => {
    loginMock.mockRejectedValue({
      isAxiosError: true,
      response: { status: 401, data: { error: 'unauthorized', message: 'Invalid credentials' } },
      message: 'Request failed',
    })
    const { wrapper } = await mountAuth()
    await wrapper.find('input[type="email"]').setValue('u@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(wrapper.text()).toContain('Неверный email или пароль')
    expect(wrapper.text()).not.toContain('Invalid credentials')
  })

  it('shows a startup hint when registration cannot reach the local API', async () => {
    registerMock.mockRejectedValue({
      isAxiosError: true,
      message: 'Network Error',
    })
    const { wrapper } = await mountAuth()
    await wrapper.find('button.auth-secondary-action').trigger('click')
    await wrapper.find('input[type="email"]').setValue('new@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('Не удалось связаться с локальным API.')
    expect(wrapper.text()).toContain('backend запущен')
    expect(wrapper.text()).not.toContain('Network Error')
  })

  it('calls register in register mode', async () => {
    registerMock.mockResolvedValue({ email: 'new@x.com', userId: 'u', verificationRequired: true })
    const { wrapper } = await mountAuth()
    await wrapper.find('button.auth-secondary-action').trigger('click')
    await wrapper.find('input[type="email"]').setValue('new@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(registerMock).toHaveBeenCalledWith('new@x.com', 'password1')
  })

  it('logs in and redirects after registration when verification is not required', async () => {
    registerMock.mockResolvedValue({ email: 'new@x.com', userId: 'u', verificationRequired: false })
    loginMock.mockResolvedValue({ accessToken: 't', email: 'new@x.com', expiresAt: '', userId: 'u', isAdmin: false })
    const { wrapper, router } = await mountAuth()
    await wrapper.find('button.auth-secondary-action').trigger('click')
    await wrapper.find('input[type="email"]').setValue('new@x.com')
    await wrapper.find('input[type="password"]').setValue('password1')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    expect(loginMock).toHaveBeenCalledWith('new@x.com', 'password1')
    expect(router.currentRoute.value.path).toBe('/')
  })
})
