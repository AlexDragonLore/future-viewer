<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import { useAuthStore } from '@/stores/useAuthStore'
import { SubscriptionStatusValue } from '@/types'

const props = defineProps<{ userId: string }>()
const emit = defineEmits<{ close: [] }>()

const store = useAdminStore()
const auth = useAuthStore()

const statusInput = ref<SubscriptionStatusValue>(SubscriptionStatusValue.None)
const expiresInput = ref<string>('')
const savingSub = ref(false)
const savingAdmin = ref(false)
const deleting = ref(false)

const isSelf = computed(() => store.selectedUser?.id === auth.userId)

watch(
  () => props.userId,
  async (id) => {
    if (id) await store.loadUserDetail(id)
  },
  { immediate: true },
)

watch(
  () => store.selectedUser,
  (u) => {
    if (!u) return
    statusInput.value = u.subscriptionStatus
    expiresInput.value = u.subscriptionExpiresAt ? u.subscriptionExpiresAt.slice(0, 16) : ''
  },
  { immediate: true },
)

async function toggleAdmin(): Promise<void> {
  if (!store.selectedUser) return
  savingAdmin.value = true
  try {
    await store.setUserAdmin(store.selectedUser.id, !store.selectedUser.isAdmin)
  } finally {
    savingAdmin.value = false
  }
}

async function saveSubscription(): Promise<void> {
  if (!store.selectedUser) return
  savingSub.value = true
  try {
    const expiresAt = expiresInput.value ? new Date(expiresInput.value).toISOString() : null
    await store.setUserSubscription(store.selectedUser.id, {
      status: statusInput.value,
      expiresAt,
    })
  } finally {
    savingSub.value = false
  }
}

async function onDelete(): Promise<void> {
  if (!store.selectedUser) return
  if (!confirm(`Удалить пользователя ${store.selectedUser.email}? Все связанные данные будут потеряны.`)) return
  deleting.value = true
  try {
    const ok = await store.deleteUser(store.selectedUser.id)
    if (ok) emit('close')
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <aside class="drawer" data-testid="admin-user-drawer">
    <button class="close" data-testid="admin-user-drawer-close" @click="emit('close')">✕</button>

    <div v-if="store.selectedUserLoading" class="empty">Загрузка…</div>
    <div v-else-if="store.selectedUserError" class="error">{{ store.selectedUserError }}</div>
    <div v-else-if="store.selectedUser" class="space-y-5">
      <header>
        <h3 class="font-display text-2xl gold-text">{{ store.selectedUser.email }}</h3>
        <p class="text-xs text-mystic-muted mt-1 mono">{{ store.selectedUser.id }}</p>
        <p class="text-xs text-mystic-muted">Создан: {{ new Date(store.selectedUser.createdAt).toLocaleString() }}</p>
      </header>

      <section class="section">
        <h4>Роль</h4>
        <div class="flex items-center gap-3">
          <span :class="['badge', store.selectedUser.isAdmin ? 'admin' : 'muted']">
            {{ store.selectedUser.isAdmin ? 'admin' : 'user' }}
          </span>
          <button
            class="admin-btn"
            :disabled="savingAdmin || isSelf"
            :title="isSelf ? 'Нельзя снять права с самого себя' : ''"
            data-testid="admin-user-toggle-admin"
            @click="toggleAdmin"
          >
            {{ store.selectedUser.isAdmin ? 'Снять права' : 'Сделать админом' }}
          </button>
        </div>
      </section>

      <section class="section">
        <h4>Подписка</h4>
        <div class="grid grid-cols-2 gap-3">
          <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
            <span>Статус</span>
            <select v-model.number="statusInput" class="admin-input" data-testid="admin-user-sub-status">
              <option :value="SubscriptionStatusValue.None">None</option>
              <option :value="SubscriptionStatusValue.Active">Active</option>
              <option :value="SubscriptionStatusValue.Expired">Expired</option>
              <option :value="SubscriptionStatusValue.Cancelled">Cancelled</option>
            </select>
          </label>
          <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
            <span>Истекает</span>
            <input
              v-model="expiresInput"
              type="datetime-local"
              class="admin-input"
              data-testid="admin-user-sub-expires"
            />
          </label>
        </div>
        <button
          class="admin-btn primary mt-2"
          :disabled="savingSub"
          data-testid="admin-user-sub-save"
          @click="saveSubscription"
        >
          {{ savingSub ? '…' : 'Сохранить подписку' }}
        </button>
      </section>

      <section class="section">
        <h4>Telegram</h4>
        <p v-if="store.selectedUser.telegramChatId" class="text-sm">
          chatId: <span class="mono">{{ store.selectedUser.telegramChatId }}</span>
        </p>
        <p v-else class="text-sm text-mystic-muted">не привязан</p>
      </section>

      <section class="section">
        <h4>Статистика</h4>
        <div class="grid grid-cols-3 gap-2 text-sm">
          <div><span class="text-mystic-muted">Читок:</span> {{ store.selectedUser.totalReadings }}</div>
          <div><span class="text-mystic-muted">Фидбеков:</span> {{ store.selectedUser.totalFeedbacks }}</div>
          <div><span class="text-mystic-muted">Баллов:</span> {{ store.selectedUser.totalScore }}</div>
        </div>
      </section>

      <section class="section">
        <h4>Последние раскладки ({{ store.selectedUser.recentReadings.length }})</h4>
        <ul v-if="store.selectedUser.recentReadings.length > 0" class="list">
          <li v-for="r in store.selectedUser.recentReadings" :key="r.id">
            <span class="mono">{{ r.id.slice(0, 8) }}</span>
            <span class="text-mystic-muted"> · {{ new Date(r.createdAt).toLocaleDateString() }} · </span>
            <span class="truncate">{{ r.question }}</span>
          </li>
        </ul>
        <p v-else class="text-mystic-muted text-sm">—</p>
      </section>

      <section class="section">
        <h4>Последние фидбеки ({{ store.selectedUser.recentFeedbacks.length }})</h4>
        <ul v-if="store.selectedUser.recentFeedbacks.length > 0" class="list">
          <li v-for="f in store.selectedUser.recentFeedbacks" :key="f.id">
            <span class="mono">{{ f.id.slice(0, 8) }}</span>
            <span class="text-mystic-muted"> · status={{ f.status }} · score={{ f.aiScore ?? '—' }}</span>
          </li>
        </ul>
        <p v-else class="text-mystic-muted text-sm">—</p>
      </section>

      <section class="section">
        <h4>Ачивки ({{ store.selectedUser.achievements.length }})</h4>
        <ul v-if="store.selectedUser.achievements.length > 0" class="list">
          <li v-for="a in store.selectedUser.achievements" :key="a.id">
            <strong>{{ a.name }}</strong>
            <span class="text-mystic-muted"> ({{ a.code }})</span>
          </li>
        </ul>
        <p v-else class="text-mystic-muted text-sm">—</p>
      </section>

      <section class="section danger-zone">
        <h4>Опасная зона</h4>
        <button
          class="admin-btn danger"
          :disabled="deleting || isSelf"
          :title="isSelf ? 'Нельзя удалить самого себя' : ''"
          data-testid="admin-user-delete"
          @click="onDelete"
        >
          {{ deleting ? '…' : 'Удалить пользователя' }}
        </button>
      </section>
    </div>
  </aside>
</template>

<style scoped>
.drawer {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: min(520px, 100%);
  background: rgba(14, 10, 24, 0.96);
  border-left: 1px solid rgba(245, 194, 107, 0.25);
  padding: 1.5rem;
  overflow-y: auto;
  z-index: 50;
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.5);
}
.close {
  position: absolute;
  top: 0.75rem;
  right: 0.75rem;
  width: 2rem;
  height: 2rem;
  border-radius: 50%;
  background: rgba(245, 194, 107, 0.08);
  color: #f5c26b;
  border: 1px solid rgba(245, 194, 107, 0.3);
}
.close:hover {
  background: rgba(245, 194, 107, 0.2);
}
.section {
  padding: 0.75rem;
  border: 1px solid rgba(245, 194, 107, 0.15);
  border-radius: 0.5rem;
  background: rgba(20, 16, 32, 0.35);
}
.section h4 {
  font-family: 'Cinzel', serif;
  font-size: 0.75rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: rgba(245, 194, 107, 0.85);
  margin-bottom: 0.5rem;
}
.list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  font-size: 0.85rem;
}
.mono {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.78rem;
}
.empty,
.error {
  text-align: center;
  padding: 2rem;
  color: rgba(224, 212, 186, 0.6);
}
.error {
  color: #ff8585;
}
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
  cursor: not-allowed;
}
.admin-btn.primary {
  background: rgba(245, 194, 107, 0.2);
  color: #f5c26b;
}
.admin-btn.danger {
  border-color: rgba(255, 80, 80, 0.5);
  color: #ff8585;
}
.admin-btn.danger:hover:not(:disabled) {
  background: rgba(255, 80, 80, 0.15);
}
.badge {
  display: inline-block;
  padding: 0.15rem 0.6rem;
  border-radius: 999px;
  font-size: 0.75rem;
  letter-spacing: 0.05em;
  border: 1px solid rgba(245, 194, 107, 0.25);
}
.badge.admin {
  background: rgba(245, 194, 107, 0.2);
  color: #f5c26b;
  border-color: rgba(245, 194, 107, 0.6);
}
.badge.muted {
  opacity: 0.5;
}
.danger-zone {
  border-color: rgba(255, 80, 80, 0.3);
}
</style>
