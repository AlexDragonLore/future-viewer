<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAdminPurchaseStore } from '@/stores/useAdminPurchaseStore'
import type { AdminPurchaseFilter, AdminPurchaseStatus } from '@/types/admin'

const store = useAdminPurchaseStore()
const route = useRoute()
const router = useRouter()
const searchInput = ref(store.search)
const statusInput = ref<AdminPurchaseFilter>(store.status)
const pageCount = computed(() => Math.max(1, Math.ceil(store.total / store.pageSize)))
const statuses: Record<AdminPurchaseStatus, string> = {
  paid: 'Оплачено', pending: 'Ожидает оплаты', canceled: 'Отменено',
  failed: 'Ошибка оплаты', refunded: 'Возврат',
}

function refresh() {
  store.setFilters({
    search: searchInput.value, status: statusInput.value,
    userId: typeof route.query.userId === 'string' ? route.query.userId : null,
  })
  void store.load()
}

watch(() => route.query.userId, refresh, { immediate: true })

function reset() {
  searchInput.value = ''
  statusInput.value = 'paid'
  if (route.query.userId) void router.replace({ name: 'admin-purchases' })
  else refresh()
}

function changePage(delta: number) {
  store.page = Math.max(1, store.page + delta)
  void store.load()
}

const dateFormat = new Intl.DateTimeFormat('ru-RU', { dateStyle: 'short', timeStyle: 'short' })
function date(value: string) { return dateFormat.format(new Date(value)) }
function money(amount: number, currency: string) {
  return new Intl.NumberFormat('ru-RU', { style: 'currency', currency }).format(amount)
}
</script>

<template>
  <section class="purchases" data-testid="admin-purchases-view">
    <header>
      <h2>История покупок подписок</h2>
      <p class="hint">Оплаты и продления по сохранённым заказам.</p>
    </header>

    <form class="filters" @submit.prevent="refresh">
      <label class="search">
        <span>Поиск</span>
        <input v-model="searchInput" type="search" placeholder="Email, номер заказа или платежа" autocomplete="off" data-testid="admin-purchase-search" />
      </label>
      <label>
        <span>Статус</span>
        <select v-model="statusInput" data-testid="admin-purchase-status" @change="refresh">
          <option value="paid">Оплаченные</option>
          <option value="all">Все статусы</option>
          <option value="pending">Ожидают оплаты</option>
          <option value="canceled">Отменённые</option>
          <option value="failed">Ошибки оплаты</option>
          <option value="refunded">Возвраты</option>
        </select>
      </label>
      <button type="submit" :disabled="store.loading" data-testid="admin-purchases-refresh">Найти / обновить</button>
      <button v-if="searchInput || statusInput !== 'paid' || store.userId" type="button" @click="reset">Сбросить</button>
    </form>

    <div v-if="store.userId" class="user-filter" data-testid="admin-purchase-user-filter">
      <span>Покупки выбранного пользователя</span>
      <RouterLink :to="{ name: 'admin-purchases' }">Все пользователи</RouterLink>
    </div>

    <p v-if="store.loading" role="status" class="empty">Загружаю историю покупок…</p>
    <div v-else-if="store.error" role="alert" class="error">
      <p>{{ store.error }}</p>
      <button type="button" @click="refresh">Повторить</button>
    </div>
    <template v-else>
      <p v-if="!store.items.length" class="empty" data-testid="admin-purchases-empty">Покупок по выбранным условиям нет.</p>
      <ol v-else class="orders" aria-label="Покупки подписок">
        <li v-for="purchase in store.items" :key="purchase.publicId" class="order" data-testid="admin-purchase-row">
          <div class="order-main">
            <div class="buyer">
              <RouterLink v-if="purchase.userId" :to="{ name: 'admin-user-detail', params: { id: purchase.userId } }">
                {{ purchase.userEmail || 'Открыть пользователя' }}
              </RouterLink>
              <span v-else class="hint">Пользователь удалён</span>
              <time :datetime="purchase.completedAt || purchase.createdAt" class="hint">
                {{ purchase.completedAt ? 'Оплата' : 'Создан' }}:
                {{ date(purchase.completedAt || purchase.createdAt) }}
              </time>
            </div>
            <div class="tariff">
              <strong>PRO · {{ purchase.accessDays }} дн.</strong>
              <span class="hint">{{ purchase.tariffCode }}</span>
            </div>
            <strong class="amount">{{ money(purchase.amount, purchase.currency) }}</strong>
            <span class="status" :class="purchase.status">{{ statuses[purchase.status] }}</span>
          </div>
          <details>
            <summary>Детали платежа</summary>
            <dl class="details">
              <div><dt>Номер заказа</dt><dd>{{ purchase.publicId }}</dd></div>
              <div><dt>Провайдер</dt><dd>{{ purchase.provider }}</dd></div>
              <div><dt>Номер платежа</dt><dd>{{ purchase.providerPaymentId || 'Ещё не присвоен' }}</dd></div>
              <div><dt>Заказ создан</dt><dd>{{ date(purchase.createdAt) }}</dd></div>
              <div><dt>Оплата подтверждена</dt><dd>{{ purchase.completedAt ? date(purchase.completedAt) : 'Не подтверждена' }}</dd></div>
            </dl>
          </details>
        </li>
      </ol>
      <nav class="pager" aria-label="Страницы покупок">
        <span data-testid="admin-purchases-total">Всего: {{ store.total }}</span>
        <div>
          <button :disabled="store.page === 1" aria-label="Предыдущая страница покупок" @click="changePage(-1)">←</button>
          <span>{{ store.page }} / {{ pageCount }}</span>
          <button :disabled="store.page >= pageCount" aria-label="Следующая страница покупок" @click="changePage(1)">→</button>
        </div>
      </nav>
    </template>
  </section>
</template>

<style scoped>
.purchases { display: grid; gap: 1rem; color: #ece3f1; font-size: 0.875rem; min-width: 0; }
h2 { font-size: 1.05rem; font-weight: 600; }
.hint { color: #a99cb9; font-size: 0.75rem; }
header .hint { margin-top: 0.3rem; }
.filters { display: flex; align-items: flex-end; flex-wrap: wrap; gap: 0.75rem; padding: 1rem; background: rgba(23, 19, 34, 0.65); border: 1px solid #332b42; border-radius: 10px; }
label { display: grid; gap: 0.35rem; font-size: 0.75rem; color: #b9abc9; min-width: 0; }
.search { flex: 1 1 260px; }
input, select, button { min-height: 44px; border: 1px solid #51435e; border-radius: 7px; padding: 0.5rem 0.75rem; }
input, select { width: 100%; background: #191320; color: #f3ebfc; }
button { color: #e1c599; }
button:disabled { opacity: 0.4; cursor: default; }
button:hover:not(:disabled) { background: #302438; }
a { color: #e1c599; overflow-wrap: anywhere; }
a:hover { text-decoration: underline; }
.user-filter, .pager { display: flex; align-items: center; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
.empty, .error { padding: 1.5rem 1rem; border: 1px solid #332b42; border-radius: 10px; color: #b9abc9; }
.error { color: #ffb3b3; display: grid; gap: 0.75rem; justify-items: start; }
.orders { display: grid; gap: 0.65rem; }
.order { border: 1px solid #332b42; background: rgba(23, 19, 34, 0.65); border-radius: 10px; padding: 0.8rem 1rem; min-width: 0; }
.order-main { display: grid; grid-template-columns: minmax(0, 1fr) minmax(110px, 0.4fr) auto 130px; gap: 1rem; align-items: center; }
.buyer, .tariff { display: grid; gap: 0.3rem; min-width: 0; overflow-wrap: anywhere; }
.amount { white-space: nowrap; font-variant-numeric: tabular-nums; }
.status { justify-self: end; border-radius: 6px; padding: 0.3rem 0.5rem; background: #30283d; font-size: 0.75rem; color: #c8b7db; }
.status.paid { color: #a4dfb4; background: #20332b; }
.status.failed, .status.canceled { color: #f3b2b2; background: #3c2932; }
.status.refunded { color: #f0d2a0; background: #3b3026; }
summary { cursor: pointer; min-height: 44px; padding-top: 0.8rem; color: #aa9aba; font-size: 0.75rem; width: fit-content; }
.details { display: grid; gap: 0.5rem; padding-top: 0.4rem; border-top: 1px solid #332b42; }
.details > div { display: grid; grid-template-columns: 160px minmax(0, 1fr); gap: 0.5rem; }
dt { color: #a99cb9; }
dd { overflow-wrap: anywhere; }
.pager { color: #b9abc9; font-size: 0.8rem; }
.pager > div { display: flex; align-items: center; gap: 0.8rem; }
@media (max-width: 640px) {
  .filters { padding: 0.75rem; }
  .filters label { flex: 1 1 100%; }
  .filters button { flex: 1; }
  .order { padding: 0.75rem; }
  .order-main { grid-template-columns: minmax(0, 1fr) auto; gap: 0.65rem; }
  .buyer { grid-column: 1 / -1; }
  .status { grid-column: 1 / -1; justify-self: start; }
  .details > div { grid-template-columns: minmax(0, 1fr); gap: 0.15rem; }
  .pager { justify-content: center; }
}
</style>
