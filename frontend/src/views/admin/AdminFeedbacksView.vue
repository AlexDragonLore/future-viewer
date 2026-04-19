<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import { FeedbackStatus } from '@/types'
import AdminFeedbacksTable from '@/components/admin/AdminFeedbacksTable.vue'
import AdminCreateFeedbackForm from '@/components/admin/AdminCreateFeedbackForm.vue'

const store = useAdminStore()
const showCreate = ref(false)

const userInput = ref('')
const statusInput = ref<FeedbackStatus | ''>('')

let userDebounce: ReturnType<typeof setTimeout> | null = null

onMounted(() => store.loadFeedbacks())

watch(userInput, (value) => {
  if (userDebounce) clearTimeout(userDebounce)
  userDebounce = setTimeout(() => {
    store.setFeedbackUserFilter(value)
    store.loadFeedbacks()
  }, 300)
})

function applyStatus(): void {
  store.setFeedbackStatusFilter(statusInput.value === '' ? null : (statusInput.value as FeedbackStatus))
  store.loadFeedbacks()
}

function nextPage(): void {
  store.setFeedbackPage(store.feedbackPage + 1)
  store.loadFeedbacks()
}

function prevPage(): void {
  store.setFeedbackPage(store.feedbackPage - 1)
  store.loadFeedbacks()
}

async function runNotifications(): Promise<void> {
  await store.runNotifications()
}
</script>

<template>
  <section class="space-y-6" data-testid="admin-feedbacks-view">
    <div class="mystic-card p-4 flex flex-wrap gap-3 items-end">
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
        <span>UserId</span>
        <input
          v-model="userInput"
          type="text"
          placeholder="UUID пользователя"
          class="admin-input"
          data-testid="admin-feedback-filter-user"
        />
      </label>
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
        <span>Статус</span>
        <select
          v-model="statusInput"
          @change="applyStatus()"
          class="admin-input"
          data-testid="admin-feedback-filter-status"
        >
          <option value="">Все</option>
          <option :value="0">Pending</option>
          <option :value="1">Notified</option>
          <option :value="2">Answered</option>
          <option :value="3">Scored</option>
        </select>
      </label>
      <div class="ml-auto flex gap-2">
        <button class="admin-btn" data-testid="admin-feedback-run-notifications" @click="runNotifications">
          Запустить рассылку
        </button>
        <button
          class="admin-btn primary"
          data-testid="admin-feedback-create"
          @click="showCreate = !showCreate"
        >
          {{ showCreate ? 'Скрыть' : 'Создать фидбек' }}
        </button>
      </div>
    </div>

    <AdminCreateFeedbackForm v-if="showCreate" @done="showCreate = false" />

    <div v-if="store.feedbackError" class="error" data-testid="admin-feedback-error">
      {{ store.feedbackError }}
    </div>
    <div v-if="store.feedbackToast" class="toast" data-testid="admin-feedback-toast">
      {{ store.feedbackToast }}
    </div>

    <AdminFeedbacksTable />

    <div class="flex justify-between items-center mt-4 text-sm text-mystic-muted">
      <span data-testid="admin-feedback-total">Всего: {{ store.feedbackTotal }}</span>
      <div class="flex gap-2 items-center">
        <button class="admin-btn" :disabled="store.feedbackPage === 1" @click="prevPage">‹</button>
        <span>Стр. {{ store.feedbackPage }}</span>
        <button
          class="admin-btn"
          :disabled="store.feedbackPage * store.feedbackPageSize >= store.feedbackTotal"
          @click="nextPage"
        >
          ›
        </button>
      </div>
    </div>
  </section>
</template>

<style scoped>
.admin-input {
  background: rgba(20, 16, 32, 0.6);
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 0.4rem;
  padding: 0.4rem 0.75rem;
  color: #f8f4eb;
  min-width: 11rem;
}
.admin-btn {
  padding: 0.45rem 0.9rem;
  border: 1px solid rgba(245, 194, 107, 0.4);
  border-radius: 0.4rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.85rem;
  transition: all 0.2s ease;
}
.admin-btn:hover:not(:disabled) {
  background: rgba(245, 194, 107, 0.1);
  color: #f5c26b;
}
.admin-btn:disabled {
  opacity: 0.4;
}
.admin-btn.primary {
  background: rgba(245, 194, 107, 0.2);
  color: #f5c26b;
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
