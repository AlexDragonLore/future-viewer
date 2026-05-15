import { describe, expect, it, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'

const publicConfigMock = vi.fn()
vi.mock('@/api/publicApi', () => ({
  publicApi: {
    config: () => publicConfigMock(),
  },
}))

import SiteFooter from '@/components/SiteFooter.vue'

async function mountFooter() {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/about', name: 'about', component: { template: '<div>about</div>' } },
      { path: '/privacy', name: 'privacy', component: { template: '<div>privacy</div>' } },
      { path: '/legal', name: 'legal', component: { template: '<div>legal</div>' } },
    ],
  })
  router.push('/about')
  await router.isReady()
  const wrapper = mount(SiteFooter, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

describe('SiteFooter', () => {
  beforeEach(() => {
    publicConfigMock.mockReset()
    publicConfigMock.mockResolvedValue({ supportEmail: 'support@example.com' })
  })

  it('links to public trust and legal pages without the old story modal', async () => {
    const wrapper = await mountFooter()

    expect(wrapper.find('a[href="/about"]').exists()).toBe(true)
    expect(wrapper.find('a[href="/privacy"]').exists()).toBe(true)
    expect(wrapper.find('a[href="/legal"]').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('Ozon')
    expect(wrapper.find('.about-overlay').exists()).toBe(false)
  })
})
