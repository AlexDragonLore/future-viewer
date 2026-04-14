import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { SpreadType } from '@/types'

vi.mock('@/api/readingApi', () => ({
  readingApi: {
    spreads: vi.fn(async () => [
      { type: SpreadType.SingleCard, name: 'One', cardCount: 1, positions: [] },
      { type: SpreadType.ThreeCard, name: 'Three', cardCount: 3, positions: [] },
      { type: SpreadType.CelticCross, name: 'Cross', cardCount: 10, positions: [] },
    ]),
    create: vi.fn(),
    get: vi.fn(),
    history: vi.fn(),
  },
}))

import HomeView from '@/views/HomeView.vue'

async function mountHome() {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: HomeView },
      { path: '/reading', name: 'reading', component: { template: '<div>reading</div>' } },
      { path: '/auth', name: 'auth', component: { template: '<div>auth</div>' } },
      { path: '/history', name: 'history', component: { template: '<div>hist</div>' } },
    ],
  })
  router.push('/')
  await router.isReady()
  const wrapper = mount(HomeView, { global: { plugins: [router] } })
  await flushPromises()
  return { wrapper, router }
}

describe('HomeView', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
  })

  it('start button is disabled when question is empty', async () => {
    const { wrapper } = await mountHome()
    const startBtn = wrapper.find('.glow-button')
    expect((startBtn.element as HTMLButtonElement).disabled).toBe(true)
  })

  it('start button enables after typing a question', async () => {
    const { wrapper } = await mountHome()
    await wrapper.find('textarea').setValue('What awaits me?')
    const startBtn = wrapper.find('.glow-button')
    expect((startBtn.element as HTMLButtonElement).disabled).toBe(false)
  })

  it('begin stores payload in sessionStorage and navigates', async () => {
    const { wrapper, router } = await mountHome()
    await wrapper.find('textarea').setValue('Question?')
    await wrapper.find('.glow-button').trigger('click')
    await flushPromises()

    const stored = sessionStorage.getItem('fv_pending')
    expect(stored).not.toBeNull()
    const parsed = JSON.parse(stored!)
    expect(parsed.question).toBe('Question?')
    expect(parsed.spreadType).toBe(SpreadType.ThreeCard)
    expect(router.currentRoute.value.name).toBe('reading')
  })

  it('selecting a spread updates active state', async () => {
    const { wrapper } = await mountHome()
    const buttons = wrapper.findAll('.spread-option')
    expect(buttons.length).toBeGreaterThanOrEqual(3)
    await buttons[0].trigger('click')
    expect(buttons[0].classes()).toContain('active')
  })
})
