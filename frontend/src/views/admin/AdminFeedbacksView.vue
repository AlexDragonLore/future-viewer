<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import { FeedbackStatus } from '@/types'
import AdminFeedbacksTable from '@/components/admin/AdminFeedbacksTable.vue'
import AdminCreateFeedbackForm from '@/components/admin/AdminCreateFeedbackForm.vue'

const store = useAdminStore()
const showCreate = ref(false)

const userInput = ref(store.feedbackUserFilter ?? '')
const statusInput = ref<FeedbackStatus | ''>(store.feedbackStatusFilter ?? '')
const pageCount = computed(() => Math.max(1, Math.ceil(store.feedbackTotal / store.feedbackPageSize)))

onMounted(() => store.loadFeedbacks())

function applyFilters(): void {
  if (store.feedbackLoading) return
  store.setFeedbackUserFilter(userInput.value)
  store.setFeedbackStatusFilter(statusInput.value === '' ? null : (statusInput.value as FeedbackStatus))
  store.loadFeedbacks()
}

function resetFilters(): void {
  userInput.value = ''
  statusInput.value = ''
  applyFilters()
}

function nextPage(): void {
  store.setFeedbackPage(store.feedbackPage + 1)
  store.loadFeedbacks()
}

function prevPage(): void {
  store.setFeedbackPage(store.feedbackPage - 1)
  store.loadFeedbacks()
}

</script>

<template>
  <section class="space-y-6" data-testid="admin-feedbacks-view">
    <form class="admin-toolbar mystic-card p-4 flex flex-wrap gap-3 items-end" @submit.prevent="applyFilters">
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1 flex-grow">
        <span>ID пользователя</span>
        <input
          v-model="userInput"
          type="text"
          placeholder="UUID пользователя"
          autocomplete="off"
          class="admin-input"
          data-testid="admin-feedback-filter-user"
        />
      </label>
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
        <span>Статус</span>
        <select
          v-model="statusInput"
          class="admin-input"
          data-testid="admin-feedback-filter-status"
        >
          <option value="">Все</option>
          <option :value="0">Ожидает ответа</option>
          <option :value="1">Уведомлён</option>
          <option :value="2">Ответ получен</option>
          <option :value="3">Оценён</option>
        </select>
      </label>
      <div class="admin-actions flex gap-2">
        <button type="submit" class="admin-btn primary" :disabled="store.feedbackLoading" data-testid="admin-feedback-apply">Применить</button>
        <button type="button" class="admin-btn" :disabled="store.feedbackLoading" data-testid="admin-feedback-reset" @click="resetFilters">Сбросить</button>
      </div>
    </form>

    <div v-if="store.feedbackError" class="error" role="alert" data-testid="admin-feedback-error">
      {{ store.feedbackError }}
    </div>
    <div v-if="store.feedbackToast" class="toast" role="status" data-testid="admin-feedback-toast">
      {{ store.feedbackToast }}
    </div>

    <AdminFeedbacksTable />

    <div class="admin-pager flex justify-between items-center mt-4 text-sm text-mystic-muted">
      <span data-testid="admin-feedback-total">Всего: {{ store.feedbackTotal }}</span>
      <div class="flex gap-2 items-center">
        <button class="admin-btn" :disabled="store.feedbackLoading || store.feedbackPage === 1" aria-label="Предыдущая страница отзывов" @click="prevPage">←</button>
        <span class="page-number">{{ store.feedbackPage }} / {{ pageCount }}</span>
        <button
          class="admin-btn"
          :disabled="store.feedbackLoading || store.feedbackPage * store.feedbackPageSize >= store.feedbackTotal"
          aria-label="Следующая страница отзывов"
          @click="nextPage"
        >
          →
        </button>
      </div>
    </div>

    <div class="advanced-actions">
      <button class="admin-btn" type="button" :aria-expanded="showCreate" aria-controls="admin-feedback-create-panel" data-testid="admin-feedback-create" @click="showCreate = !showCreate">
        {{ showCreate ? 'Скрыть создание отзыва' : 'Дополнительно: создать отзыв' }}
      </button>
      <div v-if="showCreate" id="admin-feedback-create-panel" class="mt-3">
        <AdminCreateFeedbackForm @done="showCreate = false" />
      </div>
    </div>
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
  min-width: min(11rem, 100%);
  width: 100%;
}
.admin-btn {
  min-height: 44px;
  padding: 0.45rem 0.9rem;
  border: 1px solid rgba(245, 194, 107, 0.4);
  border-radius: 0.4rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.85rem;
  transition:
    color 0.2s ease,
    background-color 0.2s ease,
    border-color 0.2s ease,
    opacity 0.2s ease;
}
.page-number {
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
}
.advanced-actions {
  border-top: 1px solid rgba(245, 194, 107, 0.15);
  padding-top: 1rem;
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
@media (max-width: 640px) {
  .admin-toolbar {
    align-items: stretch;
  }
  .admin-toolbar label,
  .admin-actions {
    flex-basis: 100%;
  }
  .admin-actions {
    margin-left: 0;
    flex-direction: column;
  }
  .admin-btn {
    width: 100%;
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
