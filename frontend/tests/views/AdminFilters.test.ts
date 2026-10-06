import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useAdminStore } from '@/stores/useAdminStore'
import { FeedbackStatus } from '@/types'
import AdminUsersView from '@/views/admin/AdminUsersView.vue'
import AdminFeedbacksView from '@/views/admin/AdminFeedbacksView.vue'

async function mountUsers() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin/users', name: 'admin-users', component: AdminUsersView },
      { path: '/admin/users/:id', name: 'admin-user-detail', component: AdminUsersView },
    ],
  })
  await router.push('/admin/users')
  return mount(AdminUsersView, {
    global: { plugins: [router], stubs: { AdminUsersTable: true, AdminUserDetailDrawer: true } },
  })
}

function mountFeedbacks() {
  return mount(AdminFeedbacksView, {
    global: { stubs: { AdminFeedbacksTable: true, AdminCreateFeedbackForm: true } },
  })
}

describe('admin filters', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('restores the user search and submits the completed query once', async () => {
    const store = useAdminStore()
    store.userSearch = 'old@example.com'
    store.userPage = 3
    const load = vi.spyOn(store, 'loadUsers').mockResolvedValue()
    const wrapper = await mountUsers()
    expect((wrapper.get('[data-testid="admin-user-search"]').element as HTMLInputElement).value).toBe('old@example.com')
    expect(load).toHaveBeenCalledTimes(1)

    await wrapper.get('[data-testid="admin-user-search"]').setValue(' new@example.com ')
    expect(load).toHaveBeenCalledTimes(1)
    await wrapper.get('form').trigger('submit')
    expect(load).toHaveBeenCalledTimes(2)
    expect(store.userSearch).toBe('new@example.com')
    expect(store.userPage).toBe(1)

    store.userLoading = true
    await wrapper.get('form').trigger('submit')
    expect(load).toHaveBeenCalledTimes(2)
  })

  it('applies both feedback filters together and resets them in one request', async () => {
    const store = useAdminStore()
    store.feedbackUserFilter = 'saved-user-id'
    store.feedbackStatusFilter = FeedbackStatus.Scored
    store.feedbackPage = 3
    const load = vi.spyOn(store, 'loadFeedbacks').mockResolvedValue()
    const wrapper = mountFeedbacks()
    expect((wrapper.get('[data-testid="admin-feedback-filter-user"]').element as HTMLInputElement).value).toBe('saved-user-id')
    expect((wrapper.get('[data-testid="admin-feedback-filter-status"]').element as HTMLSelectElement).value).toBe('3')

    await wrapper.get('[data-testid="admin-feedback-filter-user"]').setValue(' next-user-id ')
    await wrapper.get('[data-testid="admin-feedback-filter-status"]').setValue(String(FeedbackStatus.Answered))
    expect(load).toHaveBeenCalledTimes(1)
    await wrapper.get('form').trigger('submit')
    expect(load).toHaveBeenCalledTimes(2)
    expect(store.feedbackUserFilter).toBe('next-user-id')
    expect(store.feedbackStatusFilter).toBe(FeedbackStatus.Answered)
    expect(store.feedbackPage).toBe(1)

    await wrapper.get('[data-testid="admin-feedback-reset"]').trigger('click')
    expect(load).toHaveBeenCalledTimes(3)
    expect(store.feedbackUserFilter).toBeNull()
    expect(store.feedbackStatusFilter).toBeNull()
  })

  it('keeps manual feedback creation collapsed until requested', async () => {
    vi.spyOn(useAdminStore(), 'loadFeedbacks').mockResolvedValue()
    const wrapper = mountFeedbacks()
    expect(wrapper.find('admin-create-feedback-form-stub').exists()).toBe(false)
    await wrapper.get('[data-testid="admin-feedback-create"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('admin-create-feedback-form-stub').exists()).toBe(true)
    expect(wrapper.get('[data-testid="admin-feedback-create"]').attributes('aria-expanded')).toBe('true')
  })
})
