<script setup lang="ts">
import { reactive } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import { FeedbackStatus } from '@/types'
import type { AdminFeedback } from '@/types/admin'

const store = useAdminStore()

interface RowState {
  aiScore: number | null
  status: FeedbackStatus
  isSincere: boolean | null
}

const edits = reactive<Record<string, RowState>>({})
const editing = reactive<Record<string, boolean>>({})
const busy = reactive<Record<string, boolean>>({})

function toggleEditing(f: AdminFeedback): void {
  if (busy[f.id]) return
  if (!editing[f.id]) edits[f.id] = { aiScore: f.aiScore, status: f.status, isSincere: f.isSincere }
  editing[f.id] = !editing[f.id]
}

function ensureRowState(f: AdminFeedback): RowState {
  if (!edits[f.id]) {
    edits[f.id] = { aiScore: f.aiScore, status: f.status, isSincere: f.isSincere }
  }
  return edits[f.id]
}

async function saveRow(f: AdminFeedback): Promise<void> {
  if (busy[f.id]) return
  const state = ensureRowState(f)
  busy[f.id] = true
  try {
    const updated = await store.updateFeedback(f.id, {
      aiScore: state.aiScore === null || String(state.aiScore).trim() === '' ? null : state.aiScore,
      status: state.status,
      isSincere: state.isSincere,
    })
    if (updated) editing[f.id] = false
  } finally {
    busy[f.id] = false
  }
}

async function deleteRow(f: AdminFeedback): Promise<void> {
  if (busy[f.id] || !confirm(`Удалить отзыв ${f.id.slice(0, 8)}…?`)) return
  busy[f.id] = true
  try {
    await store.deleteFeedback(f.id)
  } finally {
    busy[f.id] = false
  }
}

function statusName(s: FeedbackStatus): string {
  switch (s) {
    case FeedbackStatus.Pending: return 'Ожидает ответа'
    case FeedbackStatus.Notified: return 'Уведомлён'
    case FeedbackStatus.Answered: return 'Ответ получен'
    case FeedbackStatus.Scored: return 'Оценён'
  }
}
</script>

<template>
  <div v-if="store.feedbackLoading" class="empty" data-testid="admin-feedbacks-loading">Загрузка…</div>
  <div v-else-if="store.feedbacks.length === 0" class="empty" data-testid="admin-feedbacks-empty">
    По выбранным фильтрам отзывов нет.
  </div>
  <template v-else>
    <div class="table-scroll">
      <table class="admin-table" data-testid="admin-feedbacks-table">
        <thead>
          <tr>
            <th class="mobile-hide">Создан</th>
            <th>Пользователь и отзыв</th>
            <th>Статус</th>
            <th>Баллы</th>
            <th class="mobile-hide">Искренность</th>
            <th>Действия</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="f in store.feedbacks" :key="f.id" data-testid="admin-feedback-row">
            <td class="mono mobile-hide">{{ new Date(f.createdAt).toLocaleString() }}</td>
            <td class="feedback-text-cell">
              <strong>{{ f.userEmail || f.userId.slice(0, 8) }}</strong>
              <div class="feedback-id mono">Расклад {{ f.readingId.slice(0, 8) }}</div>
              <details v-if="f.question || f.selfReport || f.aiScoreReason" class="feedback-content" data-testid="admin-feedback-content">
                <summary>Читать вопрос и отзыв</summary>
                <div v-if="f.question"><span>Вопрос</span><p>{{ f.question }}</p></div>
                <div v-if="f.selfReport"><span>Отзыв</span><p>{{ f.selfReport }}</p></div>
                <div v-if="f.aiScoreReason"><span>Обоснование оценки</span><p>{{ f.aiScoreReason }}</p></div>
              </details>
              <p v-else class="feedback-id">Текст отзыва пока отсутствует.</p>
            </td>
            <td>
              <select v-if="editing[f.id]" v-model="ensureRowState(f).status" class="cell-input" aria-label="Статус отзыва" :disabled="busy[f.id]">
                <option :value="0">Ожидает ответа</option>
                <option :value="1">Уведомлён</option>
                <option :value="2">Ответ получен</option>
                <option :value="3">Оценён</option>
              </select>
              <span v-else>{{ statusName(f.status) }}</span>
            </td>
            <td>
              <input
                v-if="editing[f.id]"
                v-model.number="ensureRowState(f).aiScore"
                type="number"
                min="1"
                max="10"
                class="cell-input score-input"
                aria-label="Оценка отзыва от 1 до 10"
                :disabled="busy[f.id]"
                data-testid="admin-feedback-score-input"
              />
              <span v-else>{{ f.aiScore ?? '—' }}</span>
            </td>
            <td class="mobile-hide">
              <select v-if="editing[f.id]" v-model="ensureRowState(f).isSincere" class="cell-input" aria-label="Искренность отзыва" :disabled="busy[f.id]">
                <option :value="null">Не оценена</option>
                <option :value="true">Да</option>
                <option :value="false">Нет</option>
              </select>
              <span v-else>{{ f.isSincere === null ? '—' : f.isSincere ? 'Да' : 'Нет' }}</span>
            </td>
            <td class="actions">
              <button v-if="editing[f.id]" class="row-btn" :disabled="busy[f.id]" data-testid="admin-feedback-save" @click="saveRow(f)">{{ busy[f.id] ? 'Сохраняю…' : 'Сохранить' }}</button>
              <button class="row-btn" :disabled="busy[f.id]" :aria-expanded="Boolean(editing[f.id])" data-testid="admin-feedback-edit" @click="toggleEditing(f)">{{ editing[f.id] ? 'Отмена' : 'Изменить' }}</button>
              <button class="row-btn danger" :disabled="busy[f.id]" data-testid="admin-feedback-delete" @click="deleteRow(f)">Удалить</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <ul class="feedback-cards" data-testid="admin-feedbacks-mobile-list">
      <li v-for="f in store.feedbacks" :key="`mobile-${f.id}`" class="feedback-card">
        <div class="feedback-head">
          <strong>{{ f.userEmail || f.userId.slice(0, 8) }}</strong>
          <span class="mono">{{ new Date(f.createdAt).toLocaleDateString() }}</span>
        </div>
        <div class="feedback-id mono">Расклад {{ f.readingId.slice(0, 8) }}</div>
        <div class="feedback-summary">{{ statusName(f.status) }} · Оценка: {{ f.aiScore ?? '—' }}</div>
        <details v-if="f.question || f.selfReport || f.aiScoreReason" class="feedback-content" data-testid="admin-feedback-content">
          <summary>Читать вопрос и отзыв</summary>
          <div v-if="f.question"><span>Вопрос</span><p>{{ f.question }}</p></div>
          <div v-if="f.selfReport"><span>Отзыв</span><p>{{ f.selfReport }}</p></div>
          <div v-if="f.aiScoreReason"><span>Обоснование оценки</span><p>{{ f.aiScoreReason }}</p></div>
        </details>
        <p v-else class="feedback-id">Текст отзыва пока отсутствует.</p>
        <div v-if="editing[f.id]" class="mobile-controls">
          <label>
            <span>Статус</span>
            <select v-model="ensureRowState(f).status" class="cell-input" :disabled="busy[f.id]">
              <option :value="0">Ожидает ответа</option>
              <option :value="1">Уведомлён</option>
              <option :value="2">Ответ получен</option>
              <option :value="3">Оценён</option>
            </select>
          </label>
          <label>
            <span>Оценка от 1 до 10</span>
            <input
              v-model.number="ensureRowState(f).aiScore"
              type="number"
              min="1"
              max="10"
              class="cell-input score-input"
              :disabled="busy[f.id]"
            />
          </label>
          <label>
            <span>Искренность</span>
            <select v-model="ensureRowState(f).isSincere" class="cell-input" :disabled="busy[f.id]">
              <option :value="null">Не оценена</option>
              <option :value="true">Да</option>
              <option :value="false">Нет</option>
            </select>
          </label>
        </div>
        <div class="actions mobile-actions">
          <button v-if="editing[f.id]" class="row-btn" :disabled="busy[f.id]" @click="saveRow(f)">{{ busy[f.id] ? 'Сохраняю…' : 'Сохранить' }}</button>
          <button class="row-btn" :disabled="busy[f.id]" :aria-expanded="Boolean(editing[f.id])" data-testid="admin-feedback-edit-mobile" @click="toggleEditing(f)">{{ editing[f.id] ? 'Отмена' : 'Изменить' }}</button>
          <button class="row-btn danger" :disabled="busy[f.id]" @click="deleteRow(f)">Удалить</button>
        </div>
      </li>
    </ul>
  </template>
</template>

<style scoped>
.empty {
  text-align: center;
  padding: 2rem;
  color: rgba(224, 212, 186, 0.6);
  font-style: italic;
}
.admin-table {
  width: 100%;
  border-collapse: collapse;
  font-family: 'Inter', system-ui, sans-serif;
}
.admin-table thead th {
  padding: 0.65rem 0.5rem;
  text-align: left;
  font-family: 'Cinzel', serif;
  font-size: 0.7rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: rgba(245, 194, 107, 0.8);
  border-bottom: 1px solid rgba(245, 194, 107, 0.25);
}
.admin-table tbody tr {
  border-bottom: 1px solid rgba(245, 194, 107, 0.08);
}
.admin-table tbody tr:hover {
  background: rgba(245, 194, 107, 0.04);
}
.admin-table td {
  padding: 0.5rem 0.5rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.85rem;
  vertical-align: top;
}
.feedback-text-cell {
  width: 38%;
  min-width: 14rem;
  overflow-wrap: anywhere;
}
.feedback-id {
  margin-top: 0.25rem;
  color: rgba(224, 212, 186, 0.55);
  font-size: 0.75rem;
}
.feedback-summary {
  margin-top: 0.65rem;
  color: rgba(245, 194, 107, 0.85);
  font-size: 0.82rem;
}
.feedback-content {
  margin-top: 0.65rem;
  overflow-wrap: anywhere;
  font-size: 0.85rem;
  line-height: 1.6;
}
.feedback-content summary {
  min-height: 44px;
  padding: 0.6rem 0;
  color: #f5c26b;
  cursor: pointer;
}
.feedback-content > div {
  margin-top: 0.65rem;
}
.feedback-content span {
  color: rgba(224, 212, 186, 0.55);
  font-size: 0.75rem;
}
.feedback-content p {
  white-space: pre-wrap;
}
.mono {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.78rem;
}
.cell-input {
  min-height: 44px;
  max-width: 100%;
  background: rgba(20, 16, 32, 0.6);
  border: 1px solid rgba(245, 194, 107, 0.2);
  border-radius: 0.3rem;
  padding: 0.25rem 0.4rem;
  color: #f8f4eb;
  font-size: 0.85rem;
}
.score-input {
  width: 4rem;
}
.actions {
  text-align: right;
}
.row-btn {
  min-height: 44px;
  padding: 0.25rem 0.45rem;
  border-radius: 0.3rem;
  margin-left: 0.25rem;
  border: 1px solid rgba(245, 194, 107, 0.3);
  background: rgba(245, 194, 107, 0.05);
  cursor: pointer;
}
.actions .row-btn {
  margin-bottom: 0.3rem;
}
.row-btn:disabled {
  opacity: 0.5;
  cursor: wait;
}
.row-btn:hover {
  background: rgba(245, 194, 107, 0.15);
}
.row-btn.danger:hover {
  background: rgba(255, 80, 80, 0.15);
  border-color: rgba(255, 80, 80, 0.6);
}
.table-scroll {
  width: 100%;
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}
.feedback-cards {
  display: none;
}
@media (max-width: 768px) {
  .table-scroll {
    display: none;
  }
  .feedback-cards {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    padding: 0;
    margin: 0;
    list-style: none;
  }
  .feedback-card {
    padding: 0.85rem;
    border: 1px solid rgba(245, 194, 107, 0.18);
    border-radius: 10px;
    background: rgba(0, 0, 0, 0.2);
  }
  .feedback-head {
    display: flex;
    flex-wrap: wrap;
    justify-content: space-between;
    gap: 0.75rem;
    color: rgba(224, 212, 186, 0.92);
  }
  .feedback-head strong {
    overflow-wrap: anywhere;
  }
  .feedback-id {
    margin-top: 0.25rem;
    color: rgba(224, 212, 186, 0.55);
  }
  .mobile-controls {
    display: grid;
    grid-template-columns: 1fr;
    gap: 0.55rem;
    margin-top: 0.75rem;
  }
  .mobile-controls label {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    color: rgba(224, 212, 186, 0.58);
    font-size: 0.72rem;
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }
  .cell-input {
    width: 100%;
  }
  .mobile-actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: stretch;
    gap: 0.5rem;
    margin-top: 0.75rem;
  }
  .mobile-actions .row-btn {
    flex: 1 1 6rem;
    margin-left: 0;
  }
  .mobile-hide {
    display: none;
  }
  .admin-table td,
  .admin-table thead th {
    padding: 0.45rem 0.35rem;
    font-size: 0.8rem;
  }
  .score-input {
    width: 100%;
  }
}
</style>
