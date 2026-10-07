import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
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
  question: 'На что обратить внимание сегодня?',
  expiresAt: '2026-10-07T12:00:00Z',
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
  })

  it('shows an anonymous question and its expiry without a user action', () => {
    const store = useAdminStore()
    store.readings = [guestReading]
    store.readingTotal = 1
    const wrapper = mountReadings()
    const row = wrapper.get('[data-testid="admin-reading-row"]')

    expect(row.get('[data-testid="admin-reading-guest"]').text()).toBe('Анонимный гость')
    expect(row.get('[data-testid="admin-reading-question"]').text()).toBe(guestReading.question)
    expect(row.find('[data-testid="admin-reading-user"]').exists()).toBe(false)
    expect(wrapper.find('admin-user-detail-drawer-stub').exists()).toBe(false)
    const retention = row.get('[data-testid="admin-reading-retention"]')
    expect(retention.text()).toContain('Хранится 24 часа')
    expect(retention.get('time').attributes('datetime')).toBe(guestReading.expiresAt)
    expect(retention.get('time').text()).toBe(new Date(guestReading.expiresAt!).toLocaleString('ru-RU'))
    expect(wrapper.text()).toContain('Анонимные сообщения хранятся 24 часа.')
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

})
