<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import { TarotPlusSessionStatus } from '@/types'
import type { AdminTarotPlusSession } from '@/types/admin'

const store = useAdminStore()
const userInput = ref('')
const statusInput = ref<TarotPlusSessionStatus | ''>('')
let userDebounce: ReturnType<typeof setTimeout> | null = null

onMounted(() => store.loadTarotPlusSessions())

watch(userInput, (value) => {
  if (userDebounce) clearTimeout(userDebounce)
  userDebounce = setTimeout(() => {
    store.setTarotPlusUserFilter(value)
    store.loadTarotPlusSessions()
  }, 300)
})

function applyStatus(): void {
  store.setTarotPlusStatusFilter(statusInput.value === '' ? null : (statusInput.value as TarotPlusSessionStatus))
  store.loadTarotPlusSessions()
}

function nextPage(): void {
  store.setTarotPlusPage(store.tarotPlusPage + 1)
  store.loadTarotPlusSessions()
}

function prevPage(): void {
  store.setTarotPlusPage(store.tarotPlusPage - 1)
  store.loadTarotPlusSessions()
}

function statusLabel(status: TarotPlusSessionStatus): string {
  switch (status) {
    case TarotPlusSessionStatus.Draft: return 'Draft'
    case TarotPlusSessionStatus.PreviewReady: return 'Preview'
    case TarotPlusSessionStatus.PaymentPending: return 'Payment'
    case TarotPlusSessionStatus.Paid: return 'Paid'
    case TarotPlusSessionStatus.Intake: return 'Intake'
    case TarotPlusSessionStatus.CardsDrawn: return 'Cards'
    case TarotPlusSessionStatus.ReportGenerating: return 'Generating'
    case TarotPlusSessionStatus.ReportReady: return 'Ready'
    case TarotPlusSessionStatus.Completed: return 'Completed'
    case TarotPlusSessionStatus.Expired: return 'Expired'
    case TarotPlusSessionStatus.Cancelled: return 'Cancelled'
    default: return String(status)
  }
}

function statusClass(status: TarotPlusSessionStatus): string {
  if (status === TarotPlusSessionStatus.ReportReady || status === TarotPlusSessionStatus.Completed) return 'ready'
  if (status === TarotPlusSessionStatus.Paid || status === TarotPlusSessionStatus.Intake) return 'paid'
  if (status === TarotPlusSessionStatus.PaymentPending) return 'pending'
  if (status === TarotPlusSessionStatus.Expired || status === TarotPlusSessionStatus.Cancelled) return 'danger'
  return 'muted'
}

function userLabel(session: AdminTarotPlusSession): string {
  return session.userEmail || session.userId
}
</script>

<template>
  <section class="space-y-6" data-testid="admin-tarot-plus-view">
    <div class="admin-toolbar mystic-card p-4 flex flex-wrap gap-3 items-end">
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
        <span>UserId</span>
        <input
          v-model="userInput"
          type="text"
          placeholder="UUID пользователя"
          class="admin-input"
          data-testid="admin-tarot-plus-filter-user"
        />
      </label>
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
        <span>Статус</span>
        <select
          v-model="statusInput"
          class="admin-input"
          data-testid="admin-tarot-plus-filter-status"
          @change="applyStatus()"
        >
          <option value="">Все</option>
          <option :value="TarotPlusSessionStatus.PreviewReady">Preview</option>
          <option :value="TarotPlusSessionStatus.PaymentPending">Payment pending</option>
          <option :value="TarotPlusSessionStatus.Paid">Paid</option>
          <option :value="TarotPlusSessionStatus.Intake">Intake</option>
          <option :value="TarotPlusSessionStatus.CardsDrawn">Cards drawn</option>
          <option :value="TarotPlusSessionStatus.ReportGenerating">Generating</option>
          <option :value="TarotPlusSessionStatus.ReportReady">Report ready</option>
          <option :value="TarotPlusSessionStatus.Completed">Completed</option>
          <option :value="TarotPlusSessionStatus.Expired">Expired</option>
          <option :value="TarotPlusSessionStatus.Cancelled">Cancelled</option>
        </select>
      </label>
      <button
        class="admin-btn ml-auto"
        :disabled="store.tarotPlusLoading"
        data-testid="admin-tarot-plus-refresh"
        @click="store.loadTarotPlusSessions()"
      >
        {{ store.tarotPlusLoading ? 'Загрузка…' : 'Обновить' }}
      </button>
    </div>

    <div v-if="store.tarotPlusError" class="error" data-testid="admin-tarot-plus-error">
      {{ store.tarotPlusError }}
    </div>

    <div v-if="store.tarotPlusLoading" class="empty" data-testid="admin-tarot-plus-loading">Загрузка…</div>
    <div v-else-if="store.tarotPlusSessions.length === 0" class="empty" data-testid="admin-tarot-plus-empty">
      Нет Таро+ сессий
    </div>
    <template v-else>
      <div class="table-scroll">
        <table class="admin-table" data-testid="admin-tarot-plus-table">
          <thead>
            <tr>
              <th>Сессия</th>
              <th>Пользователь</th>
              <th>Статус</th>
              <th>Ветка</th>
              <th class="num">Ответы</th>
              <th class="num">Цена</th>
              <th class="mobile-hide">AI</th>
              <th>Создано</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="s in store.tarotPlusSessions" :key="s.id">
              <td>
                <div class="mono">{{ s.id.slice(0, 8) }}</div>
                <div class="core">{{ s.coreRequest || '—' }}</div>
              </td>
              <td>
                <div>{{ userLabel(s) }}</div>
                <div class="mono text-mystic-muted">{{ s.userId.slice(0, 8) }}</div>
              </td>
              <td>
                <span class="badge" :class="statusClass(s.status)">{{ statusLabel(s.status) }}</span>
              </td>
              <td>{{ s.routeLabel }}</td>
              <td class="num">{{ s.intakeAnswerCount }}/{{ s.answerCount }}</td>
              <td class="num">{{ s.priceRub }} ₽</td>
              <td class="mobile-hide">{{ s.aiModel || '—' }}</td>
              <td>{{ new Date(s.createdAt).toLocaleDateString() }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <ul class="session-cards" data-testid="admin-tarot-plus-mobile-list">
        <li v-for="s in store.tarotPlusSessions" :key="`mobile-${s.id}`" class="session-card">
          <div class="mobile-card-head">
            <strong>{{ s.routeLabel }}</strong>
            <span class="badge" :class="statusClass(s.status)">{{ statusLabel(s.status) }}</span>
          </div>
          <div class="core">{{ s.coreRequest || '—' }}</div>
          <div class="mobile-card-grid">
            <span>Сессия</span>
            <span class="mono">{{ s.id.slice(0, 8) }}</span>
            <span>Пользователь</span>
            <span>{{ userLabel(s) }}</span>
            <span>Ответы</span>
            <strong>{{ s.intakeAnswerCount }}/{{ s.answerCount }}</strong>
            <span>Цена</span>
            <strong>{{ s.priceRub }} ₽</strong>
            <span>Создано</span>
            <span>{{ new Date(s.createdAt).toLocaleDateString() }}</span>
          </div>
        </li>
      </ul>
    </template>

    <div class="admin-pager flex justify-between items-center mt-4 text-sm text-mystic-muted">
      <span data-testid="admin-tarot-plus-total">Всего: {{ store.tarotPlusTotal }}</span>
      <div class="flex gap-2 items-center">
        <button class="admin-btn" :disabled="store.tarotPlusPage === 1" @click="prevPage">‹</button>
        <span>Стр. {{ store.tarotPlusPage }}</span>
        <button
          class="admin-btn"
          :disabled="store.tarotPlusPage * store.tarotPlusPageSize >= store.tarotPlusTotal"
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
  min-width: min(11rem, 100%);
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
}
.error {
  color: #ff8585;
  background: rgba(255, 50, 50, 0.08);
  border: 1px solid rgba(255, 50, 50, 0.2);
  padding: 0.6rem 0.9rem;
  border-radius: 0.4rem;
}
.empty {
  text-align: center;
  padding: 2rem;
  color: rgba(224, 212, 186, 0.6);
  font-style: italic;
}
.table-scroll {
  width: 100%;
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}
.admin-table {
  width: 100%;
  border-collapse: collapse;
  font-family: 'Inter', system-ui, sans-serif;
}
.admin-table th {
  padding: 0.65rem 0.5rem;
  text-align: left;
  font-family: 'Cinzel', serif;
  font-size: 0.7rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: rgba(245, 194, 107, 0.8);
  border-bottom: 1px solid rgba(245, 194, 107, 0.25);
}
.admin-table td {
  padding: 0.55rem 0.5rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.84rem;
  border-bottom: 1px solid rgba(245, 194, 107, 0.08);
  vertical-align: top;
}
.admin-table tbody tr:hover {
  background: rgba(245, 194, 107, 0.06);
}
.mono {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.78rem;
}
.num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
.core {
  max-width: 18rem;
  overflow: hidden;
  color: rgba(224, 212, 186, 0.62);
  font-size: 0.78rem;
  line-height: 1.35;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.badge {
  display: inline-block;
  padding: 0.1rem 0.5rem;
  border-radius: 999px;
  font-size: 0.7rem;
  letter-spacing: 0.05em;
  border: 1px solid rgba(245, 194, 107, 0.25);
  color: rgba(224, 212, 186, 0.8);
}
.badge.ready,
.badge.paid {
  background: rgba(80, 200, 120, 0.15);
  color: #98e09d;
  border-color: rgba(80, 200, 120, 0.4);
}
.badge.pending {
  background: rgba(245, 194, 107, 0.12);
  color: #f5c26b;
}
.badge.danger {
  border-color: rgba(255, 80, 80, 0.5);
  color: #ff8585;
}
.badge.muted {
  opacity: 0.55;
}
.session-cards {
  display: none;
}
@media (max-width: 768px) {
  .admin-toolbar {
    align-items: stretch;
  }
  .admin-toolbar label,
  .admin-toolbar .admin-btn {
    flex-basis: 100%;
  }
  .admin-toolbar .admin-btn {
    margin-left: 0;
    width: 100%;
  }
  .table-scroll {
    display: none;
  }
  .session-cards {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    padding: 0;
    margin: 0;
    list-style: none;
  }
  .session-card {
    padding: 0.85rem;
    border: 1px solid rgba(245, 194, 107, 0.18);
    border-radius: 10px;
    background: rgba(0, 0, 0, 0.2);
  }
  .mobile-card-head {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 0.6rem;
  }
  .mobile-card-grid {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    gap: 0.45rem 0.75rem;
    margin-top: 0.75rem;
    color: rgba(224, 212, 186, 0.78);
    font-size: 0.82rem;
  }
  .mobile-card-grid span:nth-child(odd) {
    color: rgba(224, 212, 186, 0.55);
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
  .admin-btn {
    width: 100%;
  }
}
</style>
