import { describe, it, expect, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'

vi.mock('@/api/glossaryApi', () => ({
  glossaryApi: {
    list: vi.fn(async () => []),
    get: vi.fn(),
  },
}))

import GlossaryView from '@/views/GlossaryView.vue'
import { DECKS } from '@/data/decks'
import { SPREADS_META } from '@/data/spreads'

async function mountGlossary() {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/glossary', name: 'glossary', component: GlossaryView }],
  })
  router.push('/glossary')
  await router.isReady()
  const wrapper = mount(GlossaryView, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}

describe('GlossaryView', () => {
  it('renders a section for every deck with anchor id', async () => {
    const wrapper = await mountGlossary()
    const list = wrapper.get('[data-testid="decks-list"]')
    const items = list.findAll('li')
    expect(items.length).toBe(DECKS.length)
    for (const d of DECKS) {
      expect(list.find(`#deck-${d.anchorId}`).exists()).toBe(true)
      expect(list.text()).toContain(d.label)
    }
  })

  it('renders a section for every spread with anchor id', async () => {
    const wrapper = await mountGlossary()
    const list = wrapper.get('[data-testid="spreads-list"]')
    const items = list.findAll('li')
    expect(items.length).toBe(SPREADS_META.length)
    for (const s of SPREADS_META) {
      expect(list.find(`#spread-${s.anchorId}`).exists()).toBe(true)
      expect(list.text()).toContain(s.label)
    }
  })
})
