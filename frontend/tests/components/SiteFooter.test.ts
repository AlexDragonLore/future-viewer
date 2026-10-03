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
import { useCookiePreferences } from '@/composables/useCookiePreferences'

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
    useCookiePreferences().resetCookiePreferences()
    publicConfigMock.mockReset()
    publicConfigMock.mockResolvedValue({ supportEmail: 'support@example.com' })
  })

  it('keeps legal pages in a closed native disclosure and cookie settings directly accessible', async () => {
    const wrapper = await mountFooter()

    const documents = wrapper.get('details')
    expect(documents.attributes('open')).toBeUndefined()
    expect(documents.get('summary').text()).toBe('Документы')
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
    ]) expect(documents.find(`a[href="${path}"]`).exists()).toBe(true)
    expect(documents.findAll('a')).toHaveLength(8)
    expect(wrapper.find('a[href="/faq"]').exists()).toBe(true)
    expect(wrapper.find('a[href="/"]').text()).toContain('Вуаль Грядущего')
    expect(wrapper.text()).not.toContain('Ozon')
    expect(wrapper.find('.about-overlay').exists()).toBe(false)
    expect(documents.find('[data-testid="change-cookie-settings"]').exists()).toBe(false)
    await wrapper.get('[data-testid="change-cookie-settings"]').trigger('click')
    expect(useCookiePreferences().settingsOpen.value).toBe(true)
    wrapper.unmount()
  })
})
