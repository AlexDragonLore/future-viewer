<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import AdminSubscriptionBadge from '@/components/admin/AdminSubscriptionBadge.vue'
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
        <p>Гостевые вопросы и ответы хранятся 30 дней, зарегистрированных пользователей — бессрочно.</p>
      </div>
      <button class="admin-button" :disabled="store.readingLoading" data-testid="admin-readings-refresh" @click="store.loadReadings(true)">Обновить</button>
    </div>

    <form class="search-toolbar mystic-card" @submit.prevent="search">
      <label for="admin-reading-search">
        <span>Поиск по email или вопросу</span>
        <input id="admin-reading-search" v-model="searchInput" type="search" maxlength="200" placeholder="Email или текст вопроса" data-testid="admin-reading-search" />
      </label>
      <button type="submit" class="admin-button primary" :disabled="store.readingLoading">Найти</button>
      <button v-if="store.readingSearch || searchInput" type="button" class="admin-button" :disabled="store.readingLoading" @click="clearSearch">Сбросить</button>
    </form>

    <p v-if="store.readingError" class="error" role="alert">{{ store.readingError }}<span v-if="store.readings.length"> Показана страница {{ store.readingDisplayedPage }}.</span></p>
    <p v-if="store.readingLoading" class="loading-status" role="status">Загружаю страницу {{ store.readingPage }}…</p>
    <p v-if="!store.readingLoading && !store.readingError && !store.readings.length" class="empty" data-testid="admin-readings-empty">{{ store.readingSearch ? 'По этому запросу сообщений нет.' : 'Сохранённых сообщений пока нет.' }}</p>
    <div v-if="store.readings.length" class="reading-list" :aria-busy="store.readingLoading">
      <article v-for="reading in store.readings" :key="reading.id" class="reading-card mystic-card" data-testid="admin-reading-row">
        <AdminReadingMessage :reading="reading">
          <template #identity>
            <button v-if="reading.userId" class="user-link" data-testid="admin-reading-user" @click="selectedUserId = reading.userId">{{ reading.userEmail ?? 'Открыть пользователя' }}</button>
            <span v-else class="guest-label" data-testid="admin-reading-guest">Анонимный гость</span>
          </template>
          <template #subscription><AdminSubscriptionBadge :subscription="reading.subscription" /></template>
        </AdminReadingMessage>
      </article>
    </div>

    <div class="reading-pager">
      <span data-testid="admin-readings-total">Всего: {{ store.readingTotal }}</span>
      <div>
        <button class="admin-button" :disabled="store.readingLoading || store.readingPage <= 1" data-testid="admin-reading-prev" aria-label="Предыдущая страница сообщений" @click="changePage(store.readingPage - 1)">← Назад</button>
        <span>Страница {{ store.readingDisplayedPage }} из {{ pageCount }}</span>
        <button class="admin-button" :disabled="store.readingLoading || store.readingPage >= pageCount" data-testid="admin-reading-next" aria-label="Следующая страница сообщений" @click="changePage(store.readingPage + 1)">Далее →</button>
      </div>
    </div>
    <AdminUserDetailDrawer v-if="selectedUserId" :user-id="selectedUserId" @close="selectedUserId = null" />
  </section>
</template>

<style scoped>
.readings-view { min-width: 0; }
.view-heading { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; margin-bottom: 0.5rem; }
.view-heading p { color: rgba(224, 212, 186, 0.6); font-size: 0.75rem; margin-top: 0.2rem; }
.search-toolbar { display: flex; align-items: end; flex-wrap: wrap; gap: 0.5rem; padding: 0.5rem; margin-bottom: 0.5rem; }
label { flex: 1 1 15rem; display: flex; flex-direction: column; gap: 0.5rem; color: rgba(224, 212, 186, 0.7); font-size: 0.8rem; }
input { background: rgba(20, 16, 32, 0.7); border: 1px solid rgba(245, 194, 107, 0.3); border-radius: 8px; padding: 0.65rem 0.85rem; color: #f8f4eb; width: 100%; min-height: 44px; font-size: 1rem; }
.admin-button { min-height: 44px; padding: 0.6rem 0.9rem; border: 1px solid rgba(245, 194, 107, 0.3); border-radius: 8px; color: #e0d4ba; font-size: 0.85rem; }
.admin-button:hover:not(:disabled), .admin-button.primary { background: rgba(245, 194, 107, 0.1); color: #f5c26b; }
.admin-button:disabled { opacity: 0.4; cursor: not-allowed; }
.admin-button:focus-visible, input:focus-visible, .user-link:focus-visible { outline: 2px solid #f5c26b; outline-offset: 3px; }
.reading-list { display: flex; flex-direction: column; gap: 0.25rem; }
.loading-status { color: #bcb0c7; font-size: 0.75rem; margin-bottom: 0.4rem; }
.reading-card { padding: 0.35rem 0.65rem 0; }
.user-link { display: inline; min-height: 28px; margin: 0; text-align: left; color: #f5c26b; font-size: 0.74rem; line-height: 1.4; overflow-wrap: anywhere; }
.user-link span { opacity: 0.5; }
.guest-label { margin-bottom: 0.25rem; color: #e0d4ba; font-size: 0.8125rem; line-height: 1.4; }
.reading-pager { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; margin-top: 1.5rem; color: rgba(224, 212, 186, 0.65); font-size: 0.8rem; }
.reading-pager > div { display: flex; align-items: center; gap: 0.75rem; }
.empty { text-align: center; padding: 2rem 0.5rem; color: rgba(224, 212, 186, 0.6); }
.error { padding: 1rem; color: #fca5a5; background: rgba(248, 113, 113, 0.08); border-radius: 8px; }
@media (max-width: 640px) {
  .view-heading { align-items: flex-start; }
  .view-heading .admin-button { flex-shrink: 0; }
  .reading-card { padding: 0.25rem 0.55rem 0; }
  .user-link { min-height: 44px; }
  .search-toolbar .admin-button { flex: 1; }
  .reading-pager { justify-content: center; }
  .reading-pager > div { width: 100%; justify-content: space-between; gap: 0.35rem; }
  .reading-pager .admin-button { padding: 0.5rem; }
}
</style>
