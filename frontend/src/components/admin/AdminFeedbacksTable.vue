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

function ensureRowState(f: AdminFeedback): RowState {
  if (!edits[f.id]) {
    edits[f.id] = { aiScore: f.aiScore, status: f.status, isSincere: f.isSincere }
  }
  return edits[f.id]
}

async function saveRow(f: AdminFeedback): Promise<void> {
  const state = ensureRowState(f)
  await store.updateFeedback(f.id, {
    aiScore: state.aiScore,
    status: state.status,
    isSincere: state.isSincere,
  })
}

async function deleteRow(f: AdminFeedback): Promise<void> {
  if (!confirm(`Удалить фидбек ${f.id.slice(0, 8)}…?`)) return
  await store.deleteFeedback(f.id)
}

function statusName(s: FeedbackStatus): string {
  switch (s) {
    case FeedbackStatus.Pending: return 'Pending'
    case FeedbackStatus.Notified: return 'Notified'
    case FeedbackStatus.Answered: return 'Answered'
    case FeedbackStatus.Scored: return 'Scored'
  }
}
</script>

<template>
  <div v-if="store.feedbackLoading" class="empty" data-testid="admin-feedbacks-loading">Загрузка…</div>
  <div v-else-if="store.feedbacks.length === 0" class="empty" data-testid="admin-feedbacks-empty">
    Нет фидбеков по фильтру
  </div>
  <template v-else>
    <div class="table-scroll">
      <table class="admin-table" data-testid="admin-feedbacks-table">
        <thead>
          <tr>
            <th class="mobile-hide">Создан</th>
            <th>Email</th>
            <th class="mobile-hide">Reading</th>
            <th>Статус</th>
            <th>Score</th>
            <th class="mobile-hide">Sincere</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="f in store.feedbacks" :key="f.id" data-testid="admin-feedback-row">
            <td class="mono mobile-hide">{{ new Date(f.createdAt).toLocaleString() }}</td>
            <td>{{ f.userEmail || f.userId.slice(0, 8) }}</td>
            <td class="mono mobile-hide">{{ f.readingId.slice(0, 8) }}</td>
            <td>
              <select v-model="ensureRowState(f).status" class="cell-input">
                <option :value="0">Pending</option>
                <option :value="1">Notified</option>
                <option :value="2">Answered</option>
                <option :value="3">Scored</option>
              </select>
              <span class="status-label">{{ statusName(f.status) }}</span>
            </td>
            <td>
              <input
                v-model.number="ensureRowState(f).aiScore"
                type="number"
                min="1"
                max="10"
                class="cell-input score-input"
                data-testid="admin-feedback-score-input"
              />
            </td>
            <td class="mobile-hide">
              <select v-model="ensureRowState(f).isSincere" class="cell-input">
                <option :value="true">Да</option>
                <option :value="false">Нет</option>
              </select>
            </td>
            <td class="actions">
              <button class="row-btn" data-testid="admin-feedback-save" @click="saveRow(f)">💾</button>
              <button class="row-btn danger" data-testid="admin-feedback-delete" @click="deleteRow(f)">🗑</button>
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
        <div class="feedback-id mono">reading {{ f.readingId.slice(0, 8) }}</div>
        <div class="mobile-controls">
          <label>
            <span>Статус</span>
            <select v-model="ensureRowState(f).status" class="cell-input">
              <option :value="0">Pending</option>
              <option :value="1">Notified</option>
              <option :value="2">Answered</option>
              <option :value="3">Scored</option>
            </select>
          </label>
          <label>
            <span>Score</span>
            <input
              v-model.number="ensureRowState(f).aiScore"
              type="number"
              min="1"
              max="10"
              class="cell-input score-input"
            />
          </label>
          <label>
            <span>Sincere</span>
            <select v-model="ensureRowState(f).isSincere" class="cell-input">
              <option :value="true">Да</option>
              <option :value="false">Нет</option>
            </select>
          </label>
        </div>
        <div class="actions mobile-actions">
          <button class="row-btn" @click="saveRow(f)">Сохранить</button>
          <button class="row-btn danger" @click="deleteRow(f)">Удалить</button>
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
  vertical-align: middle;
}
.mono {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.78rem;
}
.cell-input {
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
.status-label {
  display: none;
}
.actions {
  white-space: nowrap;
  text-align: right;
}
.row-btn {
  padding: 0.25rem 0.45rem;
  border-radius: 0.3rem;
  margin-left: 0.25rem;
  border: 1px solid rgba(245, 194, 107, 0.3);
  background: rgba(245, 194, 107, 0.05);
  cursor: pointer;
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
    justify-content: stretch;
    gap: 0.5rem;
    margin-top: 0.75rem;
  }
  .mobile-actions .row-btn {
    flex: 1;
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
    width: 3rem;
  }
}
</style>
