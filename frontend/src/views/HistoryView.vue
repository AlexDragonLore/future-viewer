<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { readingApi } from '@/api/readingApi'
import { extractApiError } from '@/api/httpClient'
import type { Reading } from '@/types'
import { Trash2 } from 'lucide-vue-next'

const readings = ref<Reading[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const deletingIds = ref<Set<string>>(new Set())

onMounted(async () => {
  try {
    readings.value = await readingApi.history()
  } catch (e) {
    error.value = extractApiError(e, 'Не удалось загрузить историю')
  } finally {
    loading.value = false
  }
})

async function deleteReading(reading: Reading) {
  if (!confirm('Удалить расклад полностью из истории? После удаления он больше не будет показываться в личном архиве.')) {
    return
  }

  deletingIds.value = new Set(deletingIds.value).add(reading.id)
  error.value = null
  try {
    await readingApi.delete(reading.id)
    readings.value = readings.value.filter((item) => item.id !== reading.id)
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
  <main class="history-page min-h-screen px-4 sm:px-6 py-12 sm:py-16 max-w-3xl mx-auto">
    <header class="mb-8 text-center">
      <div class="history-kicker text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ АРХИВ ✦</div>
      <h1 class="font-display text-4xl gold-text">История</h1>
    </header>

    <div v-if="loading" class="text-center text-mystic-silver/60">загружаю…</div>
    <div v-else-if="error" class="text-center text-red-300">{{ error }}</div>
    <div v-else-if="readings.length === 0" class="text-center text-mystic-silver/60">
      Пока что пусто. Сделай первый расклад.
    </div>

    <ul v-else class="space-y-4">
      <li v-for="r in readings" :key="r.id" class="history-item mystic-card">
        <RouterLink
          :to="{ name: 'reading-detail', params: { id: r.id } }"
          class="history-link p-5 block transition hover:border-mystic-accent/60 hover:shadow-[0_0_24px_rgba(245,194,107,0.25)]"
        >
          <div class="history-card-head flex justify-between items-start mb-2 pr-10">
            <div class="font-display text-mystic-accent">{{ r.spreadName }}</div>
            <div class="text-xs text-mystic-silver/50">{{ new Date(r.createdAt).toLocaleString() }}</div>
          </div>
          <p class="italic text-mystic-silver/70 mb-2">«{{ r.question }}»</p>
          <p class="text-sm text-mystic-silver/90 line-clamp-3">{{ r.interpretation }}</p>
        </RouterLink>
        <button
          type="button"
          class="delete-reading"
          :disabled="deletingIds.has(r.id)"
          aria-label="Удалить расклад полностью"
          data-testid="delete-reading"
          @click="deleteReading(r)"
        >
          <Trash2 :size="16" aria-hidden="true" />
        </button>
      </li>
    </ul>

    <div class="text-center mt-8">
      <RouterLink to="/" class="glow-button inline-block">Новый расклад</RouterLink>
    </div>
  </main>
</template>

<style scoped>
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
.history-item:hover {
  border-color: rgba(245, 194, 107, 0.6);
  box-shadow: 0 0 24px rgba(245, 194, 107, 0.25);
}
.delete-reading {
  position: absolute;
  top: 1rem;
  right: 1rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  border: 1px solid rgba(252, 165, 165, 0.34);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.28);
  color: rgba(252, 165, 165, 0.92);
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
}
</style>
