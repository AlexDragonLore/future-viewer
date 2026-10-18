import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useAdminStore } from '@/stores/useAdminStore'
import AdminUsersView from '@/views/admin/AdminUsersView.vue'

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


})
