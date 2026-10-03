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
      { path: '/', name: 'home', component: { template: '<div>home</div>' } },
      { path: '/about', name: 'about', component: { template: '<div>about</div>' } },
      { path: '/legal/:document', component: { template: '<div>legal</div>' } },
      { path: '/faq', name: 'faq', component: { template: '<div>faq</div>' } },
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
    for (const path of [
      '/legal/privacy',
      '/legal/personal-data-consent',
      '/legal/cookies',
      '/legal/offer',
      '/legal/marketing-consent',
      '/legal/ai-disclaimer',
      '/legal/data-request',
      '/legal/processors',
    ]) expect(wrapper.find(`a[href="${path}"]`).exists()).toBe(true)
    expect(wrapper.find('a[href="/faq"]').exists()).toBe(true)
    expect(wrapper.find('a[href="/"]').text()).toContain('Вуаль Грядущего')
    expect(wrapper.text()).not.toContain('Ozon')
    expect(wrapper.find('.about-overlay').exists()).toBe(false)
  })
})
