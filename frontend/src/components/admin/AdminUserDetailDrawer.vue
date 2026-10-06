<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import { useAuthStore } from '@/stores/useAuthStore'
import { FeedbackStatus, SubscriptionStatusValue } from '@/types'
import AdminReadingMessage from '@/components/admin/AdminReadingMessage.vue'

const props = defineProps<{ userId: string }>()
const emit = defineEmits<{ close: [] }>()

const store = useAdminStore()
const auth = useAuthStore()

const statusInput = ref<SubscriptionStatusValue>(SubscriptionStatusValue.None)
const expiresInput = ref<string>('')
const savingSub = ref(false)
const savingAdmin = ref(false)
const deleting = ref(false)
const achievementCodeInput = ref<string>('')
const grantingAchievement = ref(false)
const revokingCode = ref<string | null>(null)
const rechecking = ref(false)

const drawer = ref<HTMLElement | null>(null)
const closeButton = ref<HTMLButtonElement | null>(null)
let opener: HTMLElement | null = null
let previousOverflow = ''

onMounted(async () => {
  opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
  previousOverflow = document.body.style.overflow
  document.body.style.overflow = 'hidden'
  await nextTick()
  closeButton.value?.focus()
})

onBeforeUnmount(() => {
  document.body.style.overflow = previousOverflow
  store.clearUserDetail()
  if (opener?.isConnected) opener.focus()
})

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    emit('close')
    return
  }
  if (event.key !== 'Tab' || !drawer.value) return
  const focusable = Array.from(drawer.value.querySelectorAll<HTMLElement>(
    'button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], summary, [tabindex="0"]',
  )).filter(element => element.getClientRects().length > 0)
  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last?.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first?.focus()
  }
}

function feedbackStatus(status: FeedbackStatus) {
  return ['Ожидает ответа', 'Уведомление отправлено', 'Получен ответ', 'Оценён'][status] ?? '—'
}

function localDateTime(value: string | null) {
  if (!value) return ''
  const date = new Date(value)
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}

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
    expiresInput.value = localDateTime(u.subscriptionExpiresAt)
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

async function grantAchievement(): Promise<void> {
  if (!store.selectedUser) return
  const code = achievementCodeInput.value.trim()
  if (!code) return
  grantingAchievement.value = true
  try {
    const ok = await store.grantAchievement(store.selectedUser.id, code)
    if (ok) achievementCodeInput.value = ''
  } finally {
    grantingAchievement.value = false
  }
}

async function revokeAchievement(code: string): Promise<void> {
  if (!store.selectedUser) return
  if (!confirm(`Снять достижение «${code}» с пользователя?`)) return
  revokingCode.value = code
  try {
    await store.revokeAchievement(store.selectedUser.id, code)
  } finally {
    revokingCode.value = null
  }
}

async function recheckAchievements(): Promise<void> {
  if (!store.selectedUser) return
  rechecking.value = true
  try {
    await store.recheckAchievements(store.selectedUser.id)
  } finally {
    rechecking.value = false
  }
}

</script>

<template>
  <Teleport to="body">
  <div class="drawer-backdrop" @click.self="emit('close')">
  <aside ref="drawer" class="drawer" role="dialog" aria-modal="true" aria-labelledby="admin-user-dialog-title" data-testid="admin-user-drawer" @keydown="handleKeydown">
    <div class="drawer-topbar">
      <h2 id="admin-user-dialog-title">Карточка пользователя</h2>
      <button ref="closeButton" class="close" aria-label="Закрыть карточку пользователя" data-testid="admin-user-drawer-close" @click="emit('close')">✕</button>
    </div>

    <div v-if="store.selectedUserLoading" class="empty">Загрузка…</div>
    <div v-else-if="store.selectedUserError" class="error">{{ store.selectedUserError }}</div>
    <div v-else-if="store.selectedUser" class="space-y-5">
      <header>
        <h3 class="font-display text-2xl gold-text">{{ store.selectedUser.email }}</h3>
        <p class="text-xs text-mystic-muted mt-1 mono">{{ store.selectedUser.id }}</p>
        <p class="text-xs text-mystic-muted">Создан: {{ new Date(store.selectedUser.createdAt).toLocaleString() }}</p>
      </header>

      <section class="section">
        <h4>Последние сообщения ({{ store.selectedUser.recentReadings.length }})</h4>
        <ul v-if="store.selectedUser.recentReadings.length" class="list recent-messages">
          <li v-for="reading in store.selectedUser.recentReadings" :key="reading.id">
            <AdminReadingMessage :reading="reading" />
          </li>
        </ul>
        <p v-else class="text-mystic-muted text-sm">Раскладов пока нет.</p>
      </section>

      <section class="section">
        <h4>Последние отзывы ({{ store.selectedUser.recentFeedbacks.length }})</h4>
        <ul v-if="store.selectedUser.recentFeedbacks.length" class="list recent-messages">
          <li v-for="feedback in store.selectedUser.recentFeedbacks" :key="feedback.id">
            <div class="feedback-meta">{{ feedbackStatus(feedback.status) }} · {{ new Date(feedback.createdAt).toLocaleDateString('ru-RU') }} · Балл: {{ feedback.aiScore ?? '—' }}</div>
            <p v-if="feedback.question" class="feedback-question">{{ feedback.question }}</p>
            <p class="feedback-report">{{ feedback.selfReport || 'Пользователь ещё не ответил.' }}</p>
            <details v-if="feedback.aiScoreReason" class="mt-2"><summary>Комментарий к оценке</summary><p class="feedback-report">{{ feedback.aiScoreReason }}</p></details>
          </li>
        </ul>
        <p v-else class="text-mystic-muted text-sm">Отзывов пока нет.</p>
      </section>

      <section class="section">
        <h4>Роль</h4>
        <div class="flex items-center gap-3">
          <span :class="['badge', store.selectedUser.isAdmin ? 'admin' : 'muted']">
            {{ store.selectedUser.isAdmin ? 'Администратор' : 'Пользователь' }}
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
        <h4>Доступ</h4>
        <div class="grid grid-cols-2 gap-3">
          <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
            <span>Статус</span>
            <select v-model.number="statusInput" class="admin-input" data-testid="admin-user-sub-status">
              <option :value="SubscriptionStatusValue.None">Бесплатный</option>
              <option :value="SubscriptionStatusValue.Active">Активный</option>
              <option :value="SubscriptionStatusValue.Expired">Истёк</option>
              <option :value="SubscriptionStatusValue.Cancelled">Отменён</option>
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
          {{ savingSub ? '…' : 'Сохранить доступ' }}
        </button>
      </section>


      <section class="section">
        <h4>Статистика</h4>
        <div class="grid grid-cols-3 gap-2 text-sm">
          <div><span class="text-mystic-muted">Раскладов:</span> {{ store.selectedUser.totalReadings }}</div>
          <div><span class="text-mystic-muted">Отзывов:</span> {{ store.selectedUser.totalFeedbacks }}</div>
          <div><span class="text-mystic-muted">Баллов:</span> {{ store.selectedUser.totalScore }}</div>
        </div>
      </section>

      <section class="section">
        <h4>Достижения ({{ store.selectedUser.achievements.length }})</h4>
        <ul v-if="store.selectedUser.achievements.length > 0" class="list">
          <li
            v-for="a in store.selectedUser.achievements"
            :key="a.id"
            class="flex items-center gap-2"
          >
            <strong>{{ a.name }}</strong>
            <span class="text-mystic-muted"> ({{ a.code }})</span>
            <button
              class="admin-btn danger ml-auto"
              :disabled="revokingCode === a.code"
              :data-testid="`admin-user-achievement-revoke-${a.code}`"
              @click="revokeAchievement(a.code)"
            >
              {{ revokingCode === a.code ? '…' : 'Снять' }}
            </button>
          </li>
        </ul>
        <p v-else class="text-mystic-muted text-sm">—</p>
        <div class="flex items-center gap-2 mt-2">
          <input
            v-model="achievementCodeInput"
            type="text"
            class="admin-input flex-1"
            placeholder="код достижения (например first_reading)"
            data-testid="admin-user-achievement-input"
          />
          <button
            class="admin-btn"
            :disabled="grantingAchievement || !achievementCodeInput.trim()"
            data-testid="admin-user-achievement-grant"
            @click="grantAchievement"
          >
            {{ grantingAchievement ? '…' : 'Выдать' }}
          </button>
        </div>
        <button
          class="admin-btn mt-2"
          :disabled="rechecking"
          data-testid="admin-user-achievement-recheck"
          @click="recheckAchievements"
        >
          {{ rechecking ? '…' : 'Пересчитать' }}
        </button>
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
  </div>
  </Teleport>
</template>

<style scoped>
.drawer-backdrop { position: fixed; inset: 0; z-index: 80; background: rgba(0, 0, 0, 0.55); }
.drawer-topbar { position: sticky; top: -1.5rem; z-index: 1; display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin: -1.5rem -1.5rem 1.2rem; padding: 0.75rem 1.5rem; background: #100b1d; border-bottom: 1px solid rgba(245, 194, 107, 0.2); }
.drawer-topbar h2 { color: #f5c26b; font-size: 0.9rem; }
.recent-messages > li + li { border-top: 1px solid rgba(245, 194, 107, 0.15); padding-top: 1rem; margin-top: 1rem; }
.feedback-meta { color: rgba(224, 212, 186, 0.6); font-size: 0.75rem; }
.feedback-question { margin: 0.65rem 0; font-style: italic; color: #f5c26b; white-space: pre-wrap; }
.feedback-report { white-space: pre-wrap; line-height: 1.6; margin-top: 0.5rem; }
summary { color: #f5c26b; cursor: pointer; }
.drawer :focus-visible { outline: 2px solid #f5c26b; outline-offset: 3px; }
.drawer {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: min(680px, 100%);
  background: rgba(14, 10, 24, 0.96);
  border-left: 1px solid rgba(245, 194, 107, 0.25);
  padding: 1.5rem;
  overflow-y: auto;
  z-index: 50;
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.5);
  overflow-wrap: anywhere;
}
.close {
  flex: 0 0 44px;
  width: 44px;
  height: 44px;
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
  width: 100%;
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
@media (max-width: 640px) {
  .drawer {
    width: 100%;
    padding: 1rem;

    border-left: none;
  }
  .drawer-topbar { top: -1rem; margin: -1rem -1rem 1rem; padding: 0.65rem 1rem; }
  .section :deep(.grid),
  .section .grid {
    grid-template-columns: 1fr;
  }
  .section :deep(.flex),
  .section .flex {
    flex-wrap: wrap;
  }
  .admin-btn {
    width: 100%;
  }
  .section .admin-btn.ml-auto {
    margin-left: 0;
  }
  .list li {
    overflow-wrap: anywhere;
  }
}
</style>
