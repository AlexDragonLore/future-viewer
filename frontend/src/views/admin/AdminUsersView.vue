<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAdminStore } from '@/stores/useAdminStore'
import AdminUsersTable from '@/components/admin/AdminUsersTable.vue'
import AdminUserDetailDrawer from '@/components/admin/AdminUserDetailDrawer.vue'

const store = useAdminStore()
const route = useRoute()
const router = useRouter()
const searchInput = ref(store.userSearch ?? '')
const pageCount = computed(() => Math.max(1, Math.ceil(store.userTotal / store.userPageSize)))
const openUserId = computed<string | null>(() => {
  const id = route.params.id
  return typeof id === 'string' && id.length > 0 ? id : null
})
onMounted(() => store.loadUsers())

function refresh(): void {
  if (store.userLoading) return
  if ((searchInput.value.trim() || null) !== store.userSearch) store.setUserSearch(searchInput.value)
  store.loadUsers()
}

function clearSearch(): void {
  searchInput.value = ''
  refresh()
}

function nextPage(): void {
  store.setUserPage(store.userPage + 1)
  store.loadUsers()
}

function prevPage(): void {
  store.setUserPage(store.userPage - 1)
  store.loadUsers()
}

function onSelect(id: string): void {
  router.push({ name: 'admin-user-detail', params: { id } })
}

function onCloseDrawer(): void {
  store.clearUserDetail()
  router.replace({ name: 'admin-users' })
  store.loadUsers()
}
</script>

<template>
  <section class="space-y-6" data-testid="admin-users-view">
    <form class="admin-toolbar mystic-card p-4 flex flex-wrap gap-3 items-end" @submit.prevent="refresh">
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1 flex-grow">
        <span>Поиск пользователя</span>
        <input
          v-model="searchInput"
          type="search"
          placeholder="Email или его часть"
          autocomplete="off"
          class="admin-input"
          data-testid="admin-user-search"
        />
      </label>
      <button class="admin-btn" type="submit" :disabled="store.userLoading" data-testid="admin-users-refresh">
        {{ store.userLoading ? 'Загрузка…' : 'Найти / обновить' }}
      </button>
      <button v-if="searchInput || store.userSearch" class="admin-btn" type="button" :disabled="store.userLoading" data-testid="admin-users-reset" @click="clearSearch">Сбросить</button>
    </form>

    <div v-if="store.userError" class="error" role="alert" data-testid="admin-user-error">{{ store.userError }}</div>
    <div v-if="store.userToast" class="toast" role="status" data-testid="admin-user-toast">{{ store.userToast }}</div>

    <AdminUsersTable @select="onSelect" />

    <div class="admin-pager flex justify-between items-center mt-4 text-sm text-mystic-muted">
      <span data-testid="admin-user-total">Всего: {{ store.userTotal }}</span>
      <div class="flex gap-2 items-center">
        <button class="admin-btn" :disabled="store.userLoading || store.userPage === 1" aria-label="Предыдущая страница пользователей" @click="prevPage">←</button>
        <span class="page-number">{{ store.userPage }} / {{ pageCount }}</span>
        <button
          class="admin-btn"
          :disabled="store.userLoading || store.userPage * store.userPageSize >= store.userTotal"
          aria-label="Следующая страница пользователей"
          @click="nextPage"
        >
          →
        </button>
      </div>
    </div>

    <AdminUserDetailDrawer v-if="openUserId" :user-id="openUserId" @close="onCloseDrawer" />
  </section>
</template>

<style scoped>
.admin-input {
  min-height: 44px;
  background: rgba(20, 16, 32, 0.6);
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 0.4rem;
  padding: 0.4rem 0.75rem;
  color: #f8f4eb;
  width: 100%;
}
.admin-btn {
  min-height: 44px;
  padding: 0.45rem 0.9rem;
  border: 1px solid rgba(245, 194, 107, 0.4);
  border-radius: 0.4rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.85rem;
}
.page-number {
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
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
@media (max-width: 640px) {
  .admin-toolbar {
    align-items: stretch;
  }
  .admin-toolbar label {
    flex-basis: 100%;
  }
  .admin-pager {
    align-items: stretch;
    flex-direction: column;
    gap: 0.75rem;
    text-align: center;
  }
  .admin-pager > div {
    justify-content: center;
  }
}
</style>
