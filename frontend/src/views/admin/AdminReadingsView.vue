<script setup lang="ts">
import { ArrowRight, ChevronDown, RefreshCw, Search, X } from 'lucide-vue-next'
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import type { AdminReadingAudience } from '@/types/admin'
import AdminSubscriptionBadge from '@/components/admin/AdminSubscriptionBadge.vue'
import AdminReadingMessage from '@/components/admin/AdminReadingMessage.vue'
import AdminUserDetailDrawer from '@/components/admin/AdminUserDetailDrawer.vue'

const store = useAdminStore()
const searchInput = ref(store.readingSearch ?? '')
const audiences = [
  { value: 'all', label: 'Все авторы' },
  { value: 'subscribers', label: 'Подписчики' },
  { value: 'registered', label: 'С аккаунтом' },
  { value: 'guests', label: 'Гости' },
] as const
const selectedUserId = ref<string | null>(null)
const readingList = ref<HTMLElement | null>(null)
let pageNavigation = 0
const pageCount = computed(() => Math.max(1, Math.ceil(store.readingTotal / store.readingPageSize)))

onMounted(() => store.loadReadings())
watch([() => store.readingRevision, () => store.readings, () => store.readingLoading], () => {
  void store.markDisplayedReadingsViewed()
}, { flush: 'post' })

function changeUnseenOnly(value: boolean) {
  if (store.readingUnseenOnly === value) return
  pageNavigation++
  store.setReadingUnseenOnly(value)
  void store.loadReadings()
}

function changeSort(event: Event) {
  pageNavigation++
  store.setReadingSort((event.target as HTMLSelectElement).value as 'newest' | 'oldest')
  void store.loadReadings()
}

function search() {
  pageNavigation++
  store.setReadingSearch(searchInput.value)
  store.loadReadings()
}

function changeAudience(event: Event) {
  const value = (event.target as HTMLSelectElement).value as AdminReadingAudience
  if (store.readingAudience === value) return
  pageNavigation++
  store.setReadingAudience(value)
  void store.loadReadings()
}

function clearSearch() {
  searchInput.value = ''
  search()
}

async function changePage(page: number) {
  const navigation = ++pageNavigation
  store.setReadingPage(page)
  await store.loadReadings()
  if (navigation !== pageNavigation || store.readingLoading || store.readingError || store.readingDisplayedPage !== page) return
  await nextTick()
  if (navigation !== pageNavigation) return
  readingList.value?.scrollIntoView({ block: 'start', behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' })
}

function refresh() {
  pageNavigation++
  void store.loadReadings(true)
}
</script>

<template>
  <section class="readings-view" data-testid="admin-readings-view">
    <div class="view-heading">
      <h2>Сообщения</h2>
      <button class="toolbar-action" type="button" :disabled="store.readingLoading" aria-label="Обновить сообщения" title="Обновить сообщения" data-testid="admin-readings-refresh" @click="refresh"><RefreshCw :size="17" :class="{ refreshing: store.readingLoading }" aria-hidden="true" /></button>
    </div>

    <form class="search-toolbar" @submit.prevent="search">
      <label class="sr-only" for="admin-reading-search">Поиск по email или вопросу</label>
      <Search class="search-leading" :size="17" aria-hidden="true" />
      <input id="admin-reading-search" v-model="searchInput" type="search" maxlength="200" placeholder="Вопрос или email" data-testid="admin-reading-search" />
      <button v-if="store.readingSearch || searchInput" type="button" class="toolbar-action clear-search" :disabled="store.readingLoading" aria-label="Сбросить поиск" title="Сбросить поиск" @click="clearSearch"><X :size="16" aria-hidden="true" /></button>
      <button type="submit" class="search-submit" :disabled="store.readingLoading" aria-label="Найти" title="Найти"><ArrowRight :size="18" aria-hidden="true" /></button>
    </form>

    <div class="message-filters" data-testid="admin-reading-filters">
      <div class="review-toggle" role="group" aria-label="Просмотр сообщений">
        <button type="button" :aria-pressed="store.readingUnseenOnly" data-testid="admin-reading-unseen" @click="changeUnseenOnly(true)">Непрочитанные</button>
        <button type="button" :aria-pressed="!store.readingUnseenOnly" data-testid="admin-reading-seen-all" @click="changeUnseenOnly(false)">Все сообщения</button>
      </div>
      <div class="filter-fields">
        <div class="filter-field">
          <label for="admin-reading-audience">Авторы</label>
          <span class="filter-select">
            <select id="admin-reading-audience" :value="store.readingAudience" data-testid="admin-reading-audience" @change="changeAudience">
              <option v-for="audience in audiences" :key="audience.value" :value="audience.value">{{ audience.label }}</option>
            </select>
            <ChevronDown :size="14" aria-hidden="true" />
          </span>
        </div>
        <div class="filter-field">
          <label for="admin-reading-sort">Порядок</label>
          <span class="filter-select">
            <select id="admin-reading-sort" :value="store.readingSort" data-testid="admin-reading-sort" @change="changeSort">
              <option value="oldest">Сначала старые</option>
              <option value="newest">Сначала новые</option>
            </select>
            <ChevronDown :size="14" aria-hidden="true" />
          </span>
        </div>
      </div>
    </div>
    <p v-if="store.readingUnseenOnly" class="review-hint">Сообщения на открытой странице отмечаются прочитанными.</p>
    <p v-if="store.readingSeenError" class="error" role="alert">{{ store.readingSeenError }} <button type="button" class="admin-button" :disabled="store.readingSeenLoading" @click="store.markDisplayedReadingsViewed()">Повторить</button></p>
    <p v-if="store.readingError" class="error" role="alert">{{ store.readingError }}<span v-if="store.readings.length"> Показана страница {{ store.readingDisplayedPage }}.</span></p>
    <p v-if="store.readingLoading" class="loading-status" role="status">Загружаю страницу {{ store.readingPage }}…</p>
    <p v-if="!store.readingLoading && !store.readingError && !store.readings.length" class="empty" data-testid="admin-readings-empty">{{ store.readingUnseenOnly ? 'Непрочитанных сообщений по выбранным фильтрам нет.' : store.readingSearch ? 'По этому запросу сообщений нет.' : store.readingAudience !== 'all' ? 'По выбранному фильтру сообщений нет.' : 'Сохранённых сообщений пока нет.' }}</p>
    <div v-if="store.readings.length" ref="readingList" class="reading-list" :aria-busy="store.readingLoading">
      <article v-for="reading in store.readings" :key="reading.id" class="reading-card" data-testid="admin-reading-row">
        <AdminReadingMessage :reading="reading">
          <template #identity>
            <button v-if="reading.userId" class="user-link" :title="reading.userEmail ?? undefined" data-testid="admin-reading-user" @click="selectedUserId = reading.userId">{{ reading.userEmail ?? 'Открыть пользователя' }}</button>
            <span v-else class="guest-label" data-testid="admin-reading-guest" title="Анонимный гость">Гость</span>
          </template>
          <template #subscription><AdminSubscriptionBadge v-if="reading.userId" :subscription="reading.subscription" compact @open="selectedUserId = reading.userId" /></template>
        </AdminReadingMessage>
      </article>
    </div>

    <div class="reading-pager">
      <span data-testid="admin-readings-total">{{ store.readingUnseenOnly ? 'Непрочитанных на момент загрузки:' : 'Всего:' }} {{ store.readingTotal }}</span>
      <div v-if="!store.readingUnseenOnly">
        <button class="admin-button" :disabled="store.readingLoading || store.readingPage <= 1" data-testid="admin-reading-prev" aria-label="Предыдущая страница сообщений" @click="changePage(store.readingPage - 1)">← Назад</button>
        <span>Страница {{ store.readingDisplayedPage }} из {{ pageCount }}</span>
        <button class="admin-button" :disabled="store.readingLoading || store.readingPage >= pageCount" data-testid="admin-reading-next" aria-label="Следующая страница сообщений" @click="changePage(store.readingPage + 1)">Далее →</button>
      </div>
      <button v-else type="button" class="admin-button" data-testid="admin-reading-next-unseen" :disabled="store.readingLoading || store.readingSeenLoading || !!store.readingSeenError || store.readingTotal <= store.readings.length" @click="changePage(1)">Следующие непрочитанные →</button>
    </div>
    <AdminUserDetailDrawer v-if="selectedUserId" :user-id="selectedUserId" @close="selectedUserId = null" />
  </section>
</template>

<style scoped>
.readings-view { min-width: 0; }
.view-heading { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; margin-bottom: 0.6rem; }
.view-heading h2 { font-family: inherit; color: #e9e1ef; font-size: 1.1rem; line-height: 1.4; font-weight: 600; }
.toolbar-action { display: inline-flex; align-items: center; justify-content: center; width: 40px; height: 40px; flex-shrink: 0; border-radius: 9px; color: #b8abc8; }
.toolbar-action:hover:not(:disabled) { background: rgba(198, 178, 224, 0.08); color: #eadbc3; }
.search-toolbar { display: flex; align-items: center; gap: 0.4rem; min-width: 0; padding: 0.25rem 0.3rem 0.25rem 0.85rem; margin-bottom: 0.85rem; background: rgba(27, 20, 42, 0.85); border: 1px solid rgba(180, 158, 210, 0.18); border-radius: 11px; }
.message-filters { display: flex; align-items: end; gap: 1rem; margin-bottom: 0.65rem; }
.review-toggle { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); flex: 1; min-width: 0; padding: 3px; border: 1px solid rgba(180, 158, 210, 0.12); border-radius: 11px; background: rgba(12, 8, 22, 0.5); }
.review-toggle button { min-height: 44px; padding: 0.5rem 0.65rem; border: 1px solid transparent; border-radius: 8px; color: #b6a8c5; font-size: 0.8125rem; line-height: 1.3; }
.review-toggle button:hover { color: #eee5f3; }
.review-toggle button[aria-pressed="true"] { color: #f0d4a3; border-color: rgba(214, 183, 131, 0.2); background: rgba(214, 183, 131, 0.12); box-shadow: 0 1px 4px rgba(0, 0, 0, 0.15); }
.filter-fields { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); flex: 1; min-width: 0; gap: 0.6rem; }
.filter-field { display: flex; flex-direction: column; min-width: 0; gap: 0.3rem; color: #a99bb9; font-size: 0.6875rem; }
.filter-field > label { padding-left: 0.15rem; }
.filter-select { position: relative; display: block; }
.filter-select select { appearance: none; width: 100%; min-height: 44px; padding: 0.5rem 1.75rem 0.5rem 0.65rem; border: 1px solid rgba(180, 158, 210, 0.18); border-radius: 9px; background: #1b142a; color: #dfd3e8; font-size: 0.8125rem; line-height: 1.3; cursor: pointer; }
.filter-select select:hover { border-color: rgba(214, 183, 131, 0.4); }
.filter-select select:focus-visible { outline: 2px solid #d6b783; outline-offset: 2px; }
.filter-select svg { position: absolute; right: 0.6rem; top: 50%; transform: translateY(-50%); pointer-events: none; color: #a99bb9; }
.review-hint { margin: 0 0 0.85rem; color: #a99bb9; font-size: 0.6875rem; line-height: 1.5; }
.search-toolbar:focus-within { border-color: rgba(214, 183, 131, 0.55); box-shadow: 0 0 0 2px rgba(214, 183, 131, 0.06); }
.search-leading { color: #897d99; flex-shrink: 0; }
input { min-width: 0; flex: 1; background: transparent; border: 0; padding: 0.5rem 0.2rem; color: #f3edf6; min-height: 42px; font-size: 1rem; outline: none; }
input::placeholder { color: #968aa6; }
input::-webkit-search-cancel-button { display: none; }
.search-submit { display: inline-flex; align-items: center; justify-content: center; min-width: 40px; min-height: 40px; border-radius: 8px; color: #e1c397; background: rgba(220, 183, 120, 0.09); }
.search-submit:hover:not(:disabled) { background: rgba(220, 183, 120, 0.16); }
.admin-button { min-height: 40px; padding: 0.5rem 0.8rem; border: 1px solid rgba(180, 158, 210, 0.18); border-radius: 9px; color: #d7c9e1; font-size: 0.8rem; }
.admin-button:hover:not(:disabled) { background: rgba(198, 178, 224, 0.08); }
button:disabled { opacity: 0.4; cursor: not-allowed; }
button:focus-visible { outline: 2px solid #d6b783; outline-offset: 2px; }
.reading-list { scroll-margin-top: 80px; display: flex; flex-direction: column; gap: 0.45rem; }
.loading-status { color: #bcb0c7; font-size: 0.75rem; margin-bottom: 0.4rem; }
.reading-card { padding: 0.4rem 0.75rem 0.25rem; border: 1px solid rgba(176, 151, 203, 0.12); border-radius: 11px; background: rgba(27, 19, 43, 0.74); }
.reading-card:hover, .reading-card:focus-within { border-color: rgba(194, 170, 220, 0.23); background: rgba(32, 23, 49, 0.84); }
.user-link { display: block; width: 100%; min-width: 0; min-height: 28px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; margin: 0; text-align: left; color: #d7b983; font-size: 0.75rem; font-weight: 500; line-height: 1.4; }
.guest-label { color: #a7bfe0; font-size: 0.75rem; font-weight: 500; line-height: 1.4; }
.reading-pager { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.75rem; margin-top: 1rem; color: #a89ab7; font-size: 0.75rem; }
.reading-pager > div { display: flex; align-items: center; gap: 0.6rem; }
.empty { text-align: center; padding: 2rem 0.5rem; color: #a89ab7; }
.error { padding: 0.8rem; color: #fca5a5; background: rgba(248, 113, 113, 0.08); border-radius: 9px; }
.refreshing { animation: refresh-spin 1.2s linear infinite; }
@keyframes refresh-spin { to { transform: rotate(360deg); } }
@media (max-width: 640px) {
  .message-filters { flex-direction: column; align-items: stretch; gap: 0.65rem; }
  .reading-card { padding: 0.2rem 0.65rem 0.3rem; }
  .user-link { min-height: 44px; }
  .toolbar-action, .search-submit { min-height: 44px; min-width: 44px; }
  .clear-search { width: 32px; min-width: 32px; }
  .reading-pager { justify-content: center; }
  .reading-pager > div { width: 100%; justify-content: space-between; gap: 0.35rem; }
}
@media (prefers-reduced-motion: reduce) { .refreshing { animation: none; } }
</style>
