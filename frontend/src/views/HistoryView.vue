<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'
import { readingApi } from '@/api/readingApi'
import { privacyApi } from '@/api/privacyApi'
import { extractApiError } from '@/api/httpClient'
import type { Reading } from '@/types'
import { Trash2 } from 'lucide-vue-next'
import { useReadingStore } from '@/stores/useReadingStore'

const readings = ref<Reading[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const deletingIds = ref<Set<string>>(new Set())
const pendingDeletion = ref<Reading | null>(null)
const deletionPassword = ref('')
const passwordInput = ref<HTMLInputElement | null>(null)

async function confirmDeletion(reading: Reading) {
  pendingDeletion.value = reading
  deletionPassword.value = ''
  error.value = null
  await nextTick()
  passwordInput.value?.focus()
}

function cancelDeletion() {
  pendingDeletion.value = null
  deletionPassword.value = ''
}

onMounted(async () => {
  try {
    readings.value = await readingApi.history()
  } catch (e) {
    error.value = extractApiError(e, 'Не удалось загрузить историю')
  } finally {
    loading.value = false
  }
})

async function deleteReading() {
  const reading = pendingDeletion.value
  if (!reading || deletingIds.value.has(reading.id)) return
  const password = deletionPassword.value
  if (password.length < 8) {
    error.value = 'Удаление отменено: требуется повторная аутентификация текущим паролем.'
    return
  }

  deletingIds.value = new Set(deletingIds.value).add(reading.id)
  error.value = null
  try {
    await privacyApi.deleteReading(reading.id, password)
    const activeReading = useReadingStore()
    if (activeReading.current?.id === reading.id) activeReading.reset()
    readings.value = readings.value.filter((item) => item.id !== reading.id)
    cancelDeletion()
  } catch (e) {
    error.value = extractApiError(e, 'Не удалось удалить расклад')
  } finally {
    deletionPassword.value = ''
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
    <div v-if="error" role="alert" class="text-center text-red-300 mb-4">{{ error }}</div>
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
          <p class="italic text-mystic-silver/70 mb-2">«{{ r.question }}»</p>
          <p class="text-sm text-mystic-silver/90 line-clamp-3">{{ r.interpretation }}</p>
        </RouterLink>
        <div class="history-actions">
          <button
            type="button"
            class="delete-reading"
            :disabled="deletingIds.has(r.id)"
            aria-label="Удалить расклад полностью"
            data-testid="delete-reading"
            @click="confirmDeletion(r)"
          >
            <Trash2 :size="16" aria-hidden="true" />
            <span>{{ deletingIds.has(r.id) ? 'Удаляю...' : 'Удалить из истории' }}</span>
          </button>
        </div>
      </li>
    </ul>

    <form v-if="pendingDeletion" class="mystic-card deletion-confirmation mt-5 p-5" data-testid="delete-reading-form" @submit.prevent="deleteReading">
      <h2 class="text-mystic-accent mb-2">Удалить выбранный расклад?</h2>
      <p class="text-sm text-mystic-silver/70 mb-3">«{{ pendingDeletion.question }}» будет удалён вместе со связанными данными. Действие нельзя отменить.</p>
      <label for="reading-deletion-password" class="text-sm">Текущий пароль</label>
      <input
        id="reading-deletion-password"
        ref="passwordInput"
        v-model="deletionPassword"
        type="password"
        autocomplete="current-password"
        required
        minlength="8"
        :disabled="deletingIds.has(pendingDeletion.id)"
        class="w-full my-3 p-3 rounded-lg bg-black/30 border border-mystic-accent/30"
        data-testid="reading-deletion-password"
      />
      <div class="flex flex-wrap gap-3">
        <button type="submit" class="delete-reading" :disabled="deletingIds.has(pendingDeletion.id)" data-testid="confirm-delete-reading">Подтвердить удаление</button>
        <button type="button" :disabled="deletingIds.has(pendingDeletion.id)" data-testid="cancel-delete-reading" @click="cancelDeletion">Отмена</button>
      </div>
    </form>

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
