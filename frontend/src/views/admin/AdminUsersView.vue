<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import AdminUsersTable from '@/components/admin/AdminUsersTable.vue'
import AdminUserDetailDrawer from '@/components/admin/AdminUserDetailDrawer.vue'

const store = useAdminStore()
const searchInput = ref('')
const openUserId = ref<string | null>(null)
let searchDebounce: ReturnType<typeof setTimeout> | null = null

onMounted(() => store.loadUsers())

watch(searchInput, (value) => {
  if (searchDebounce) clearTimeout(searchDebounce)
  searchDebounce = setTimeout(() => {
    store.setUserSearch(value)
    store.loadUsers()
  }, 300)
})

function nextPage(): void {
  store.setUserPage(store.userPage + 1)
  store.loadUsers()
}

function prevPage(): void {
  store.setUserPage(store.userPage - 1)
  store.loadUsers()
}

function onSelect(id: string): void {
  openUserId.value = id
}

function onCloseDrawer(): void {
  openUserId.value = null
  store.clearUserDetail()
  store.loadUsers()
}
</script>

<template>
  <section class="space-y-6" data-testid="admin-users-view">
    <div class="mystic-card p-4 flex flex-wrap gap-3 items-end">
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1 flex-grow">
        <span>Email</span>
        <input
          v-model="searchInput"
          type="text"
          placeholder="поиск по email"
          class="admin-input"
          data-testid="admin-user-search"
        />
      </label>
    </div>

    <div v-if="store.userError" class="error" data-testid="admin-user-error">{{ store.userError }}</div>
    <div v-if="store.userToast" class="toast" data-testid="admin-user-toast">{{ store.userToast }}</div>

    <AdminUsersTable @select="onSelect" />

    <div class="flex justify-between items-center mt-4 text-sm text-mystic-muted">
      <span data-testid="admin-user-total">Всего: {{ store.userTotal }}</span>
      <div class="flex gap-2 items-center">
        <button class="admin-btn" :disabled="store.userPage === 1" @click="prevPage">‹</button>
        <span>Стр. {{ store.userPage }}</span>
        <button
          class="admin-btn"
          :disabled="store.userPage * store.userPageSize >= store.userTotal"
          @click="nextPage"
        >
          ›
        </button>
      </div>
    </div>

    <AdminUserDetailDrawer v-if="openUserId" :user-id="openUserId" @close="onCloseDrawer" />
  </section>
</template>

<style scoped>
.admin-input {
  background: rgba(20, 16, 32, 0.6);
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 0.4rem;
  padding: 0.4rem 0.75rem;
  color: #f8f4eb;
}
.admin-btn {
  padding: 0.45rem 0.9rem;
  border: 1px solid rgba(245, 194, 107, 0.4);
  border-radius: 0.4rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.85rem;
}
.admin-btn:hover:not(:disabled) {
  background: rgba(245, 194, 107, 0.1);
  color: #f5c26b;
}
.admin-btn:disabled {
  opacity: 0.4;
}
.error {
  color: #ff8585;
  background: rgba(255, 50, 50, 0.08);
  border: 1px solid rgba(255, 50, 50, 0.2);
  padding: 0.6rem 0.9rem;
  border-radius: 0.4rem;
}
.toast {
  color: #98e09d;
  background: rgba(80, 200, 120, 0.08);
  border: 1px solid rgba(80, 200, 120, 0.2);
  padding: 0.6rem 0.9rem;
  border-radius: 0.4rem;
}
</style>
