import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { DeckType, SpreadType, type Reading } from '@/types'

const historyMock = vi.fn()
const deleteMock = vi.fn()

vi.mock('@/api/readingApi', () => ({
  readingApi: {
    spreads: vi.fn(),
    create: vi.fn(),
    get: vi.fn(),
    history: () => historyMock(),
    delete: (id: string) => deleteMock(id),
  },
}))

import HistoryView from '@/views/HistoryView.vue'

async function mountHistory() {
  setActivePinia(createPinia())
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div>h</div>' } },
      { path: '/history', name: 'history', component: HistoryView },
      { path: '/reading/:id', name: 'reading-detail', component: { template: '<div>d</div>' } },
    ],
  })
  router.push('/history')
  await router.isReady()
  return mount(HistoryView, { global: { plugins: [router] } })
}

const sample: Reading = {
  id: 'r1',
  spreadType: SpreadType.ThreeCard,
  spreadName: 'Three card',
  question: 'where to?',
  createdAt: '2026-04-14T12:00:00Z',
  cards: [],
  interpretation: 'The stars align',
  deckType: DeckType.RWS,
}

describe('HistoryView', () => {
  beforeEach(() => {
    historyMock.mockReset()
    deleteMock.mockReset()
    deleteMock.mockResolvedValue(undefined)
    vi.spyOn(window, 'confirm').mockReturnValue(true)
  })

  it('shows loading state before the request resolves', async () => {
    historyMock.mockImplementation(() => new Promise(() => {}))
    const wrapper = await mountHistory()
    expect(wrapper.text()).toContain('загружаю')
  })

  it('renders empty state when no readings are returned', async () => {
    historyMock.mockResolvedValue([])
    const wrapper = await mountHistory()
    await flushPromises()
    expect(wrapper.text()).toContain('Пока что пусто')
  })

  it('renders the list of readings returned from API', async () => {
    historyMock.mockResolvedValue([sample])
    const wrapper = await mountHistory()
    await flushPromises()
    expect(wrapper.text()).toContain('Three card')
    expect(wrapper.text()).toContain('where to?')
    expect(wrapper.text()).toContain('The stars align')
    expect(wrapper.get('[data-testid="delete-reading"]').text()).toContain('Удалить из истории')
  })

  it('each reading item links to its detail page', async () => {
    historyMock.mockResolvedValue([sample])
    const wrapper = await mountHistory()
    await flushPromises()
    const link = wrapper.find('a[href="/reading/r1"]')
    expect(link.exists()).toBe(true)
  })

  it('shows an error when the request fails', async () => {
    historyMock.mockRejectedValue({ response: { data: { error: 'forbidden', message: 'Nope' } } })
    const wrapper = await mountHistory()
    await flushPromises()
    expect(wrapper.text()).toContain('Nope')
  })

  it('keeps the history and allows retry after a failed deletion', async () => {
    historyMock.mockResolvedValue([sample])
    deleteMock.mockRejectedValueOnce({ response: { data: { message: 'Попробуйте позже' } } })
    const wrapper = await mountHistory()
    await flushPromises()
    await wrapper.get('[data-testid="delete-reading"]').trigger('click')
    await wrapper.get('[data-testid="delete-reading-form"]').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('Попробуйте позже')
    expect(wrapper.find('a[href="/reading/r1"]').exists()).toBe(true)
    await wrapper.get('[data-testid="delete-reading-form"]').trigger('submit')
    await flushPromises()
    expect(deleteMock).toHaveBeenCalledTimes(2)
    expect(wrapper.find('a[href="/reading/r1"]').exists()).toBe(false)
  })

  it('cancels removal without sending a deletion request', async () => {
    historyMock.mockResolvedValue([sample])
    const wrapper = await mountHistory()
    await flushPromises()
    await wrapper.get('[data-testid="delete-reading"]').trigger('click')
    await wrapper.get('[data-testid="cancel-delete-reading"]').trigger('click')
    expect(deleteMock).not.toHaveBeenCalled()
    expect(wrapper.find('a[href="/reading/r1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="delete-reading-form"]').exists()).toBe(false)
  })

  it('soft-deletes a reading from the rendered history without a password', async () => {
    historyMock.mockResolvedValue([sample])
    const wrapper = await mountHistory()
    await flushPromises()

    await wrapper.find('[data-testid="delete-reading"]').trigger('click')
    expect(deleteMock).not.toHaveBeenCalled()
    expect(wrapper.find('input[type="password"]').exists()).toBe(false)
    await wrapper.get('[data-testid="delete-reading-form"]').trigger('submit')
    await flushPromises()

    expect(deleteMock).toHaveBeenCalledWith('r1')
    expect(wrapper.text()).toContain('Пока что пусто')
  })
})
