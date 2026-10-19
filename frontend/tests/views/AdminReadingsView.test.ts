import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminStore } from '@/stores/useAdminStore'
import AdminReadingsView from '@/views/admin/AdminReadingsView.vue'
import type { AdminReading } from '@/types/admin'
import { adminReadingFixtures } from '../fixtures/admin'

const savedReading: AdminReading = adminReadingFixtures[0]
const guestReading: AdminReading = {
  ...savedReading,
  id: 'guest-reading',
  userId: null,
  userEmail: null,
  subscription: null,
  question: 'На что обратить внимание сегодня?',
  expiresAt: '2026-11-05T12:00:00Z',
}

function mountReadings() {
  return mount(AdminReadingsView, {
    global: { stubs: { AdminUserDetailDrawer: true } },
  })
}

describe('AdminReadingsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.spyOn(useAdminStore(), 'loadReadings').mockResolvedValue()
    vi.spyOn(useAdminStore(), 'markDisplayedReadingsViewed').mockResolvedValue()
  })

  it('acknowledges rendered rows without opening answers and stops observing after leaving', async () => {
    const store = useAdminStore()
    const wrapper = mountReadings()
    store.readings = [savedReading, guestReading]
    await flushPromises()
    expect(store.markDisplayedReadingsViewed).toHaveBeenCalled()
    expect(wrapper.findAll('[data-testid="admin-reading-answer"]')).toHaveLength(0)
    expect(wrapper.get('[data-testid="admin-reading-unseen"]').attributes('aria-pressed')).toBe('true')
    expect((wrapper.get('[data-testid="admin-reading-sort"]').element as HTMLSelectElement).value).toBe('oldest')
    expect(store.readingUnseenOnly).toBe(true)
    expect(store.readingSort).toBe('oldest')
    expect(store.readingPage).toBe(1)
    expect(wrapper.find('[data-testid="admin-reading-prev"]').exists()).toBe(false)
    wrapper.unmount()
    vi.mocked(store.markDisplayedReadingsViewed).mockClear()
    store.readings = [savedReading]
    await flushPromises()
    expect(store.markDisplayedReadingsViewed).not.toHaveBeenCalled()
  })

  it('shows an anonymous question and its expiry without a user action', () => {
    const store = useAdminStore()
    store.readings = [guestReading]
    store.readingTotal = 1
    const wrapper = mountReadings()
    const row = wrapper.get('[data-testid="admin-reading-row"]')

    expect(row.get('[data-testid="admin-reading-guest"]').text()).toBe('Гость')
    expect(row.get('[data-testid="admin-reading-question"]').text()).toBe(guestReading.question)
    expect(row.find('[data-testid="admin-reading-user"]').exists()).toBe(false)
    expect(wrapper.find('admin-user-detail-drawer-stub').exists()).toBe(false)
    const retention = row.get('[data-testid="admin-reading-retention"]')
    expect(retention.attributes('aria-label')).toContain('30 дней')
    expect(retention.attributes('title')).toContain(new Date(guestReading.expiresAt!).toLocaleString('ru-RU'))
    expect(retention.text()).toBe('')
    expect(wrapper.text()).not.toContain('Вопросы пользователей и гостей')
    expect(wrapper.text()).not.toContain('Гостевые вопросы и ответы хранятся')
    expect(wrapper.text()).not.toContain('Без подписки')
    expect(row.get('[data-testid="admin-reading-spread"]').attributes('aria-label')).toContain('Rider–Waite–Smith')
    expect(row.text()).not.toContain('Rider–Waite–Smith')
  })

  it('keeps saved readings linked to their user without a guest expiry notice', async () => {
    useAdminStore().readings = [guestReading, savedReading]
    const wrapper = mountReadings()
    const row = wrapper.findAll('[data-testid="admin-reading-row"]')[1]

    expect(row.find('[data-testid="admin-reading-guest"]').exists()).toBe(false)
    expect(row.find('[data-testid="admin-reading-retention"]').exists()).toBe(false)
    expect(row.get('[data-testid="admin-reading-user"]').text()).toContain(savedReading.userEmail)
    await row.get('[data-testid="admin-reading-user"]').trigger('click')
    expect(wrapper.getComponent({ name: 'AdminUserDetailDrawer' }).props('userId')).toBe(savedReading.userId)
  })

  it('opens the user drawer from a compact paid subscription badge', async () => {
    useAdminStore().readings = [savedReading]
    const wrapper = mountReadings()
    const badge = wrapper.get('[data-testid="admin-subscription-button"]')
    expect(badge.text()).toBe('30д')
    expect(badge.attributes('aria-label')).toContain('499')
    await badge.trigger('click')
    expect(wrapper.getComponent({ name: 'AdminUserDetailDrawer' }).props('userId')).toBe(savedReading.userId)
  })

  it('scrolls after a successful page change but keeps the viewport on a failed request', async () => {
    const store = useAdminStore()
    store.setReadingUnseenOnly(false)
    store.readings = adminReadingFixtures.slice(0, 20)
    store.readingTotal = 41
    const scroll = vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(() => {})
    const wrapper = mountReadings()
    vi.mocked(store.loadReadings).mockImplementationOnce(async () => {
      store.readings = adminReadingFixtures.slice(20, 40)
      store.readingDisplayedPage = 2
    })
    await wrapper.get('[data-testid="admin-reading-next"]').trigger('click')
    await flushPromises()
    expect(scroll).toHaveBeenCalledOnce()
    expect(scroll).toHaveBeenCalledWith(expect.objectContaining({ block: 'start' }))
    scroll.mockClear()
    vi.mocked(store.loadReadings).mockImplementationOnce(async () => { store.readingError = 'network'; store.readingPage = 2 })
    await wrapper.get('[data-testid="admin-reading-next"]').trigger('click')
    await flushPromises()
    expect(scroll).not.toHaveBeenCalled()
    scroll.mockRestore()
  })

  it('does not scroll for a superseded page request after a new search', async () => {
    const store = useAdminStore()
    store.setReadingUnseenOnly(false)
    store.readings = adminReadingFixtures.slice(0, 20)
    store.readingTotal = 41
    const scroll = vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(() => {})
    const wrapper = mountReadings()
    let finish!: () => void
    vi.mocked(store.loadReadings).mockImplementationOnce(() => new Promise<void>(resolve => { finish = resolve }))
    await wrapper.get('[data-testid="admin-reading-next"]').trigger('click')
    await wrapper.get('[data-testid="admin-reading-search"]').setValue('new query')
    await wrapper.get('form').trigger('submit')
    store.readingDisplayedPage = 2
    finish()
    await flushPromises()
    expect(scroll).not.toHaveBeenCalled()
    scroll.mockRestore()
  })

  it('restores the audience selection and sends exactly one request when it changes', async () => {
    const store = useAdminStore()
    store.readingSearch = 'saved question'
    store.readingAudience = 'registered'
    store.readingPage = 3
    store.readings = [savedReading]
    const wrapper = mountReadings()
    const audience = wrapper.get('[data-testid="admin-reading-audience"]')
    expect((audience.element as HTMLSelectElement).value).toBe('registered')
    expect(store.loadReadings).toHaveBeenCalledTimes(1)
    await audience.setValue('subscribers')
    expect(store.readingAudience).toBe('subscribers')
    expect(store.readingPage).toBe(1)
    expect(store.readingSearch).toBe('saved question')
    expect((wrapper.get('[data-testid="admin-reading-search"]').element as HTMLInputElement).value).toBe('saved question')
    expect(store.readings).toEqual([])
    expect((audience.element as HTMLSelectElement).value).toBe('subscribers')
    expect(store.loadReadings).toHaveBeenCalledTimes(2)
    await audience.setValue('subscribers')
    expect(store.loadReadings).toHaveBeenCalledTimes(2)
    await wrapper.get('[data-testid="admin-reading-search"]').setValue('new question')
    await wrapper.get('form').trigger('submit')
    expect(store.readingSearch).toBe('new question')
    expect(store.readingAudience).toBe('subscribers')
  })

  it('describes an empty audience without implying the whole history is empty', () => {
    useAdminStore().setReadingUnseenOnly(false)
    useAdminStore().setReadingAudience('guests')
    const wrapper = mountReadings()
    expect(wrapper.get('[data-testid="admin-readings-empty"]').text()).toBe('По выбранному фильтру сообщений нет.')
  })

})
