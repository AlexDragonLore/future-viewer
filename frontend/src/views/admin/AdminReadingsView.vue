<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import AdminReadingMessage from '@/components/admin/AdminReadingMessage.vue'
import AdminUserDetailDrawer from '@/components/admin/AdminUserDetailDrawer.vue'

const store = useAdminStore()
const searchInput = ref(store.readingSearch ?? '')
const selectedUserId = ref<string | null>(null)
const pageCount = computed(() => Math.max(1, Math.ceil(store.readingTotal / store.readingPageSize)))

onMounted(() => store.loadReadings())

function search() {
  store.setReadingSearch(searchInput.value)
  store.loadReadings()
}

function clearSearch() {
  searchInput.value = ''
  search()
}

function changePage(page: number) {
  store.setReadingPage(page)
  store.loadReadings()
}
</script>

<template>
  <section class="readings-view" data-testid="admin-readings-view">
    <div class="view-heading">
      <div>
        <h2 class="font-display text-xl gold-text">Последние сообщения</h2>
        <p>Вопросы пользователей и гостей, ответы ИИ · сначала новые</p>
        <p>Анонимные сообщения хранятся 24 часа.</p>
      </div>
      <button class="admin-button" :disabled="store.readingLoading" data-testid="admin-readings-refresh" @click="store.loadReadings()">Обновить</button>
    </div>

    <form class="search-toolbar mystic-card" @submit.prevent="search">
      <label for="admin-reading-search">
        <span>Поиск по email или вопросу</span>
        <input id="admin-reading-search" v-model="searchInput" type="search" maxlength="200" placeholder="Email или текст вопроса" data-testid="admin-reading-search" />
      </label>
      <button type="submit" class="admin-button primary" :disabled="store.readingLoading">Найти</button>
      <button v-if="store.readingSearch || searchInput" type="button" class="admin-button" :disabled="store.readingLoading" @click="clearSearch">Сбросить</button>
    </form>

    <p v-if="store.readingError" class="error" role="alert">{{ store.readingError }}</p>
    <p v-else-if="store.readingLoading" class="empty" role="status">Загружаю сообщения…</p>
    <p v-else-if="!store.readings.length" class="empty" data-testid="admin-readings-empty">{{ store.readingSearch ? 'По этому запросу сообщений нет.' : 'Сохранённых сообщений пока нет.' }}</p>
    <div v-else class="reading-list">
      <article v-for="reading in store.readings" :key="reading.id" class="reading-card mystic-card" data-testid="admin-reading-row">
        <button v-if="reading.userId" class="user-link" data-testid="admin-reading-user" @click="selectedUserId = reading.userId">{{ reading.userEmail ?? 'Открыть пользователя' }} <span aria-hidden="true">↗</span></button>
        <p v-else class="guest-label" data-testid="admin-reading-guest">Анонимный гость</p>
        <AdminReadingMessage :reading="reading" />
      </article>
    </div>

    <div class="reading-pager">
      <span data-testid="admin-readings-total">Всего: {{ store.readingTotal }}</span>
      <div>
        <button class="admin-button" :disabled="store.readingLoading || store.readingPage <= 1" data-testid="admin-reading-prev" aria-label="Предыдущая страница сообщений" @click="changePage(store.readingPage - 1)">← Назад</button>
        <span>Страница {{ store.readingPage }} из {{ pageCount }}</span>
        <button class="admin-button" :disabled="store.readingLoading || store.readingPage >= pageCount" data-testid="admin-reading-next" aria-label="Следующая страница сообщений" @click="changePage(store.readingPage + 1)">Далее →</button>
      </div>
    </div>
    <AdminUserDetailDrawer v-if="selectedUserId" :user-id="selectedUserId" @close="selectedUserId = null" />
  </section>
</template>

<style scoped>
.readings-view { min-width: 0; }
.view-heading { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin-bottom: 1.25rem; }
.view-heading p { color: rgba(224, 212, 186, 0.6); font-size: 0.85rem; margin-top: 0.4rem; }
.search-toolbar { display: flex; align-items: end; flex-wrap: wrap; gap: 0.75rem; padding: 1rem; margin-bottom: 1.25rem; }
label { flex: 1 1 15rem; display: flex; flex-direction: column; gap: 0.5rem; color: rgba(224, 212, 186, 0.7); font-size: 0.8rem; }
input { background: rgba(20, 16, 32, 0.7); border: 1px solid rgba(245, 194, 107, 0.3); border-radius: 8px; padding: 0.65rem 0.85rem; color: #f8f4eb; width: 100%; min-height: 44px; font-size: 1rem; }
.admin-button { min-height: 44px; padding: 0.6rem 0.9rem; border: 1px solid rgba(245, 194, 107, 0.3); border-radius: 8px; color: #e0d4ba; font-size: 0.85rem; }
.admin-button:hover:not(:disabled), .admin-button.primary { background: rgba(245, 194, 107, 0.1); color: #f5c26b; }
.admin-button:disabled { opacity: 0.4; cursor: not-allowed; }
.admin-button:focus-visible, input:focus-visible, .user-link:focus-visible { outline: 2px solid #f5c26b; outline-offset: 3px; }
.reading-list { display: flex; flex-direction: column; gap: 1rem; }
.reading-card { padding: 1.25rem; }
.user-link { display: block; min-height: 44px; margin-bottom: 0.25rem; text-align: left; color: #f5c26b; overflow-wrap: anywhere; }
.user-link span { opacity: 0.5; }
.guest-label { margin-bottom: 0.75rem; color: #e0d4ba; font-size: 0.9rem; }
.reading-pager { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; margin-top: 1.5rem; color: rgba(224, 212, 186, 0.65); font-size: 0.8rem; }
.reading-pager > div { display: flex; align-items: center; gap: 0.75rem; }
.empty { text-align: center; padding: 2rem 0.5rem; color: rgba(224, 212, 186, 0.6); }
.error { padding: 1rem; color: #fca5a5; background: rgba(248, 113, 113, 0.08); border-radius: 8px; }
@media (max-width: 640px) {
  .view-heading { align-items: flex-start; }
  .view-heading .admin-button { flex-shrink: 0; }
  .reading-card { padding: 1rem; }
  .search-toolbar .admin-button { flex: 1; }
  .reading-pager { justify-content: center; }
  .reading-pager > div { width: 100%; justify-content: space-between; gap: 0.35rem; }
  .reading-pager .admin-button { padding: 0.5rem; }
}
</style>
