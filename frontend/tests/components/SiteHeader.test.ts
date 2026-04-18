import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import { SubscriptionStatusValue } from '@/types'

const statusMock = vi.fn()
vi.mock('@/api/subscriptionApi', () => ({
  subscriptionApi: {
    status: (...args: []) => statusMock(...args),
  },
}))

import SiteHeader from '@/components/SiteHeader.vue'

function buildRouter(): Router {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: { template: '<div>home</div>' } },
      { path: '/glossary', name: 'glossary', component: { template: '<div>glossary</div>' } },
      { path: '/history', name: 'history', component: { template: '<div>history</div>' } },
      { path: '/auth', name: 'auth', component: { template: '<div>auth</div>' } },
    ],
  })
}

async function mountHeader() {
  setActivePinia(createPinia())
  const router = buildRouter()
  router.push('/')
  await router.isReady()
  const wrapper = mount(SiteHeader, { global: { plugins: [router] } })
  await flushPromises()
  return { wrapper, router }
}

describe('SiteHeader', () => {
  beforeEach(() => {
    localStorage.clear()
    statusMock.mockReset()
    statusMock.mockResolvedValue({
      status: SubscriptionStatusValue.None,
      expiresAt: null,
      isActive: false,
      freeReadingsUsedToday: 0,
      freeReadingsDailyLimit: 1,
      canCreateFreeReading: true,
    })
  })

  it('renders logo and glossary link', async () => {
    const { wrapper } = await mountHeader()
    expect(wrapper.find('[data-testid="site-logo"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-glossary"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Глоссарий')
  })

  it('shows login link for anonymous users and hides history link', async () => {
    const { wrapper } = await mountHeader()
    expect(wrapper.find('[data-testid="nav-auth"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-history"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="user-button"]').exists()).toBe(false)
  })

  it('shows user menu and history link for authenticated users', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'someone@example.com')
    const { wrapper } = await mountHeader()
    expect(wrapper.find('[data-testid="user-button"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-history"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('someone@example.com')
  })

  it('shows remaining free quota for authenticated free user', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    const { wrapper } = await mountHeader()
    const quota = wrapper.find('[data-testid="header-quota"]')
    expect(quota.exists()).toBe(true)
    expect(quota.text()).toContain('1/1')
  })

  it('shows infinite badge for subscribed user', async () => {
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
    const { wrapper } = await mountHeader()
    const quota = wrapper.find('[data-testid="header-quota"]')
    expect(quota.exists()).toBe(true)
    expect(quota.text()).toContain('∞')
    expect(quota.classes()).toContain('subscribed')
  })

  it('opens the deck picker and selects a deck', async () => {
    const { wrapper } = await mountHeader()
    const picker = wrapper.get('[data-testid="deck-picker"]')
    expect(picker.find('.deck-menu').exists()).toBe(false)
    await picker.get('.deck-button').trigger('click')
    expect(picker.find('.deck-menu').exists()).toBe(true)
    const options = picker.findAll('.deck-option')
    expect(options.length).toBe(5)
    await options[1].trigger('click')
    expect(picker.find('.deck-menu').exists()).toBe(false)
    expect(picker.get('.deck-button').text()).toContain('Thoth')
  })

  it('logout clears auth state and routes home', async () => {
    localStorage.setItem('fv_token', 'test-token')
    localStorage.setItem('fv_email', 'u@x.com')
    const { wrapper, router } = await mountHeader()
    await router.push('/glossary')
    await flushPromises()
    await wrapper.get('[data-testid="user-button"]').trigger('click')
    await wrapper.get('[data-testid="logout-button"]').trigger('click')
    await flushPromises()
    expect(localStorage.getItem('fv_token')).toBeNull()
    expect(router.currentRoute.value.name).toBe('home')
  })
})
