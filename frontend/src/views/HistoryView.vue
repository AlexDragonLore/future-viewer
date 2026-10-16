<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { readingApi } from '@/api/readingApi'
import { extractApiError } from '@/api/httpClient'
import type { ReadingHistorySummary } from '@/types/history'
import { Trash2 } from 'lucide-vue-next'
import { useReadingStore } from '@/stores/useReadingStore'

const readings = ref<ReadingHistorySummary[]>([])
const pageSize = 20
const page = ref(1)
const total = ref(0)
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))
const heading = ref<HTMLElement | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const deletingIds = ref<Set<string>>(new Set())
const pendingDeletion = ref<ReadingHistorySummary | null>(null)
const confirmDeleteButton = ref<HTMLButtonElement | null>(null)
let initiatingDeleteButton: HTMLButtonElement | null = null

async function confirmDeletion(reading: ReadingHistorySummary, event: MouseEvent) {
  initiatingDeleteButton = event.currentTarget as HTMLButtonElement
  pendingDeletion.value = reading
  error.value = null
  await nextTick()
  confirmDeleteButton.value?.focus()
}

async function cancelDeletion() {
  pendingDeletion.value = null
  await nextTick()
  initiatingDeleteButton?.focus()
  initiatingDeleteButton = null
}

async function loadPage(requestedPage = page.value, scroll = false) {
  loading.value = true
  error.value = null
  try {
    let result = await readingApi.historySummary(requestedPage, pageSize)
    const lastPage = Math.max(1, Math.ceil(result.total / pageSize))
    if (result.items.length === 0 && requestedPage > lastPage) {
      result = await readingApi.historySummary(lastPage, pageSize)
    }
    readings.value = result.items
    total.value = result.total
    page.value = result.page
    if (scroll) {
      await nextTick()
      heading.value?.focus({ preventScroll: true })
      heading.value?.scrollIntoView({ block: 'start', behavior: 'auto' })
    }
  } catch (e) {
    error.value = extractApiError(e, 'Не удалось загрузить историю')
  } finally {
    loading.value = false
  }
}

onMounted(() => loadPage())

async function deleteReading() {
  const reading = pendingDeletion.value
  if (!reading || deletingIds.value.has(reading.id)) return
  deletingIds.value = new Set(deletingIds.value).add(reading.id)
  error.value = null
  try {
    await readingApi.delete(reading.id)
    const activeReading = useReadingStore()
    if (activeReading.current?.id === reading.id) activeReading.reset()
    readings.value = readings.value.filter((item) => item.id !== reading.id)
    total.value = Math.max(0, total.value - 1)
    page.value = Math.min(page.value, totalPages.value)
    pendingDeletion.value = null
    initiatingDeleteButton = null
    await loadPage(page.value)
  } catch (e) {
    error.value = extractApiError(e, 'Не удалось удалить расклад')
  } finally {
    const next = new Set(deletingIds.value)
    next.delete(reading.id)
    deletingIds.value = next
  }
}
</script>

<template>
  <main class="history-page w-full min-h-screen px-4 sm:px-6 py-12 sm:py-16 max-w-3xl mx-auto">
    <header ref="heading" tabindex="-1" class="history-heading mb-8 text-center">
      <div class="history-kicker text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ АРХИВ ✦</div>
      <h1 class="font-display text-4xl gold-text">История</h1>
      <p v-if="total" class="text-xs text-mystic-silver/60 mt-2">Сохранено раскладов: {{ total }}</p>
    </header>

    <div v-if="loading" class="text-center text-mystic-silver/60">загружаю…</div>
    <div v-if="error" role="alert" class="text-center text-red-300 mb-4">
      {{ error }}
      <button v-if="!pendingDeletion" type="button" class="history-page-button ml-2" :disabled="loading" @click="loadPage()">Повторить</button>
    </div>
    <div v-if="!loading && !error && readings.length === 0" class="text-center text-mystic-silver/60">
      Пока что пусто. Сделай первый расклад.
    </div>

    <ul v-if="!loading && readings.length" class="space-y-4">
      <li v-for="r in readings" :key="r.id" class="history-item mystic-card">
        <RouterLink
          :to="{ name: 'reading-detail', params: { id: r.id } }"
          class="history-link p-5 pb-3 block transition hover:border-mystic-accent/60 hover:shadow-[0_0_24px_rgba(245,194,107,0.25)]"
        >
          <div class="history-card-head flex justify-between items-start mb-2">
            <div class="font-display text-mystic-accent">{{ r.spreadName }}</div>
            <div class="text-xs text-mystic-silver/50">{{ new Date(r.createdAt).toLocaleString() }}</div>
          </div>
          <p class="history-question italic text-mystic-silver/70 mb-2">«{{ r.question }}»</p>
          <p class="text-sm text-mystic-silver/90 line-clamp-3">{{ r.interpretationPreview }}</p>
        </RouterLink>
        <div class="history-actions">
          <button
            type="button"
            class="delete-reading"
            :disabled="deletingIds.has(r.id)"
            aria-label="Удалить расклад из истории"
            data-testid="delete-reading"
            @click="confirmDeletion(r, $event)"
          >
            <Trash2 :size="16" aria-hidden="true" />
            <span>{{ deletingIds.has(r.id) ? 'Удаляю...' : 'Удалить из истории' }}</span>
          </button>
        </div>
      </li>
    </ul>

    <form v-if="pendingDeletion" class="mystic-card deletion-confirmation mt-5 p-5" data-testid="delete-reading-form" @submit.prevent="deleteReading">
      <h2 class="text-mystic-accent mb-2">Удалить расклад из истории?</h2>
      <p class="text-sm text-mystic-silver/70 mb-3">«{{ pendingDeletion.question }}» больше не будет отображаться в истории.</p>
      <div class="flex flex-wrap gap-3">
        <button ref="confirmDeleteButton" type="submit" class="delete-reading" :disabled="deletingIds.has(pendingDeletion.id)" data-testid="confirm-delete-reading">Подтвердить удаление</button>
        <button type="button" :disabled="deletingIds.has(pendingDeletion.id)" data-testid="cancel-delete-reading" @click="cancelDeletion">Отмена</button>
      </div>
    </form>

    <nav v-if="totalPages > 1" class="history-pagination mt-6" aria-label="Страницы истории">
      <button type="button" class="history-page-button" data-testid="history-previous"
        :disabled="loading || page <= 1 || pendingDeletion !== null || deletingIds.size > 0"
        @click="loadPage(page - 1, true)">Назад</button>
      <span aria-live="polite">Страница {{ page }} из {{ totalPages }}</span>
      <button type="button" class="history-page-button" data-testid="history-next"
        :disabled="loading || page >= totalPages || pendingDeletion !== null || deletingIds.size > 0"
        @click="loadPage(page + 1, true)">Далее</button>
    </nav>

    <div class="text-center mt-8">
      <RouterLink to="/" class="glow-button inline-block">Новый расклад</RouterLink>
    </div>
  </main>
</template>

<style scoped>
.history-heading {
  scroll-margin-top: 6rem;
}
.history-question {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
.history-pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  font-size: 0.8rem;
  color: rgba(224, 212, 186, 0.75);
}
.history-page-button {
  min-height: 44px;
  padding: 0.6rem 0.9rem;
  border: 1px solid rgba(245, 194, 107, 0.3);
  border-radius: 8px;
  color: #f5c26b;
}
.history-page-button:disabled {
  opacity: 0.4;
  cursor: default;
}
.history-item {
  position: relative;
  overflow: hidden;
  transition:
    border-color 0.2s ease,
    box-shadow 0.2s ease;
}
.history-link {
  text-decoration: none;
}
.deletion-confirmation p {
  overflow-wrap: anywhere;
}
.history-item:hover {
  border-color: rgba(245, 194, 107, 0.6);
  box-shadow: 0 0 24px rgba(245, 194, 107, 0.25);
}
.history-actions {
  display: flex;
  justify-content: flex-end;
  padding: 0 1.25rem 1.25rem;
}
.delete-reading {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.45rem;
  min-height: 2.25rem;
  padding: 0.45rem 0.7rem;
  border: 1px solid rgba(252, 165, 165, 0.34);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.28);
  color: rgba(252, 165, 165, 0.92);
  font-size: 0.78rem;
  line-height: 1;
  cursor: pointer;
  transition:
    background-color 0.2s ease,
    border-color 0.2s ease,
    opacity 0.2s ease;
}
.delete-reading:hover:not(:disabled) {
  border-color: rgba(252, 165, 165, 0.7);
  background: rgba(239, 68, 68, 0.14);
}
.delete-reading:disabled {
  cursor: wait;
  opacity: 0.55;
}
@media (max-width: 640px) {
  .history-page {
    padding-top: 2rem;
    padding-bottom: 2.5rem;
  }
  .history-kicker {
    letter-spacing: 0.14em;
  }
  .history-card-head {
    flex-direction: column;
    gap: 0.35rem;
  }
  p {
    overflow-wrap: anywhere;
  }
  .history-actions {
    justify-content: stretch;
    padding: 0 1.25rem 1.25rem;
  }
  .delete-reading {
    width: 100%;
  }
}
</style>
