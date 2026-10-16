import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { DeckType, SpreadType } from '@/types'
import type { ReadingHistorySummary } from '@/types/history'

const historyMock = vi.fn()
const deleteMock = vi.fn()
let serverReadings: ReadingHistorySummary[] = []

vi.mock('@/api/readingApi', () => ({
  readingApi: {
    spreads: vi.fn(),
    create: vi.fn(),
    get: vi.fn(),
    historySummary: (page: number, pageSize: number) => historyMock(page, pageSize),
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

const sample: ReadingHistorySummary = {
  id: 'r1',
  spreadType: SpreadType.ThreeCard,
  spreadName: 'Three card',
  question: 'where to?',
  createdAt: '2026-04-14T12:00:00Z',
  interpretationPreview: 'The stars align',
  deckType: DeckType.RWS,
}

describe('HistoryView', () => {
  beforeEach(() => {
    historyMock.mockReset()
    deleteMock.mockReset()
    serverReadings = []
    historyMock.mockImplementation(async (page: number, pageSize: number) => ({
      items: serverReadings.slice((page - 1) * pageSize, page * pageSize),
      total: serverReadings.length, page, pageSize,
    }))
    deleteMock.mockImplementation(async (id: string) => { serverReadings = serverReadings.filter(r => r.id !== id) })
    vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(() => {})
    vi.spyOn(window, 'confirm').mockReturnValue(true)
  })

  it('shows loading state before the request resolves', async () => {
    historyMock.mockImplementation(() => new Promise(() => {}))
    const wrapper = await mountHistory()
    expect(wrapper.text()).toContain('загружаю')
  })

  it('renders empty state when no readings are returned', async () => {
    const wrapper = await mountHistory()
    await flushPromises()
    expect(wrapper.text()).toContain('Пока что пусто')
  })

  it('renders the list of readings returned from API', async () => {
    serverReadings = [sample]
    const wrapper = await mountHistory()
    await flushPromises()
    expect(wrapper.text()).toContain('Three card')
    expect(wrapper.text()).toContain('where to?')
    expect(wrapper.text()).toContain('The stars align')
    expect(wrapper.get('[data-testid="delete-reading"]').text()).toContain('Удалить из истории')
  })

  it('each reading item links to its detail page', async () => {
    serverReadings = [sample]
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
    serverReadings = [sample]
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
    serverReadings = [sample]
    const wrapper = await mountHistory()
    await flushPromises()
    await wrapper.get('[data-testid="delete-reading"]').trigger('click')
    await wrapper.get('[data-testid="cancel-delete-reading"]').trigger('click')
    expect(deleteMock).not.toHaveBeenCalled()
    expect(wrapper.find('a[href="/reading/r1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="delete-reading-form"]').exists()).toBe(false)
  })

  it('soft-deletes a reading from the rendered history without a password', async () => {
    serverReadings = [sample]
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

  it('pages through history beyond fifty entries and retains full question text', async () => {
    serverReadings = Array.from({ length: 61 }, (_, i) => ({ ...sample, id: `r${i}`, question: `Question ${i} ${'long '.repeat(80)}` }))
    const wrapper = await mountHistory()
    await flushPromises()
    expect(historyMock).toHaveBeenLastCalledWith(1, 20)
    expect(wrapper.findAll('.history-item')).toHaveLength(20)
    expect(wrapper.text()).toContain(serverReadings[0]!.question)
    await wrapper.get('[data-testid="history-next"]').trigger('click')
    await flushPromises()
    expect(historyMock).toHaveBeenLastCalledWith(2, 20)
    expect(wrapper.text()).toContain('Страница 2 из 4')
    expect(Element.prototype.scrollIntoView).toHaveBeenCalledWith({ block: 'start', behavior: 'auto' })
    await wrapper.get('[data-testid="history-next"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="history-next"]').trigger('click')
    await flushPromises()
    expect(wrapper.findAll('.history-item')).toHaveLength(1)
    expect(wrapper.find('a[href="/reading/r60"]').exists()).toBe(true)
  })

  it('returns to the preceding page and refreshes it after removing the last row', async () => {
    serverReadings = Array.from({ length: 21 }, (_, i) => ({ ...sample, id: `r${i}`, question: `Question ${i}` }))
    const wrapper = await mountHistory()
    await flushPromises()
    await wrapper.get('[data-testid="history-next"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="delete-reading"]').trigger('click')
    await wrapper.get('[data-testid="delete-reading-form"]').trigger('submit')
    await flushPromises()
    expect(deleteMock).toHaveBeenCalledWith('r20')
    expect(historyMock).toHaveBeenLastCalledWith(1, 20)
    expect(wrapper.findAll('.history-item')).toHaveLength(20)
    expect(wrapper.find('a[href="/reading/r20"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="history-next"]').exists()).toBe(false)
  })

  it('keeps the current page on a paging failure and allows retry', async () => {
    serverReadings = Array.from({ length: 21 }, (_, i) => ({ ...sample, id: `r${i}` }))
    const wrapper = await mountHistory()
    await flushPromises()
    historyMock.mockRejectedValueOnce(new Error('offline'))
    await wrapper.get('[data-testid="history-next"]').trigger('click')
    await flushPromises()
    expect(wrapper.findAll('.history-item')).toHaveLength(20)
    expect(wrapper.text()).toContain('Страница 1 из 2')
    await wrapper.get('[data-testid="history-next"]').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Страница 2 из 2')
  })
})
