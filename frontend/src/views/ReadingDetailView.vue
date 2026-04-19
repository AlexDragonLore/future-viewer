<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { readingApi } from '@/api/readingApi'
import { extractApiError } from '@/api/httpClient'
import CardFlip from '@/components/cards/CardFlip.vue'
import { marked } from 'marked'
import type { Reading } from '@/types'

const route = useRoute()
const reading = ref<Reading | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

async function load(id: string) {
  loading.value = true
  error.value = null
  reading.value = null
  try {
    reading.value = await readingApi.get(id)
  } catch (e) {
    error.value = extractApiError(e, 'Не удалось загрузить расклад')
  } finally {
    loading.value = false
  }
}

watch(
  () => route.params.id,
  (id) => {
    if (typeof id === 'string' && id) load(id)
  },
  { immediate: true },
)

const interpretationHtml = computed(() =>
  reading.value?.interpretation ? (marked.parse(reading.value.interpretation) as string) : '',
)
</script>

<template>
  <main class="min-h-screen px-6 py-16 flex flex-col items-center">
    <div class="w-full max-w-3xl mb-6">
      <RouterLink to="/history" class="text-sm text-mystic-accent/80 hover:text-mystic-accent transition">
        ← К истории
      </RouterLink>
    </div>

    <div v-if="loading" class="text-center text-mystic-silver/60">загружаю…</div>
    <div v-else-if="error" class="text-center text-red-300">{{ error }}</div>
    <div v-else-if="!reading" class="text-center text-mystic-silver/60">Расклад не найден.</div>

    <template v-else>
      <header class="text-center mb-10">
        <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ {{ reading.spreadName.toUpperCase() }} ✦</div>
        <h1 class="font-display text-4xl md:text-5xl gold-text">Архивный расклад</h1>
        <p class="text-mystic-silver/60 mt-2 italic">«{{ reading.question }}»</p>
        <p class="text-xs text-mystic-silver/40 mt-1">{{ new Date(reading.createdAt).toLocaleString() }}</p>
      </header>

      <section class="cards-grid mb-12">
        <div v-for="card in reading.cards" :key="card.position" class="card-entry">
          <div class="text-xs text-mystic-accent/80 uppercase tracking-widest mb-2 text-center">
            {{ card.positionName }}
          </div>
          <CardFlip :card="card" :face-up="true" :width="130" />
          <div class="text-xs text-mystic-silver/60 mt-2 text-center max-w-[140px]">
            {{ card.meaning }}
          </div>
        </div>
      </section>

      <section v-if="reading.interpretation" class="mystic-card max-w-2xl w-full p-8 mb-8">
        <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Интерпретация</div>
        <div class="prose-mystic text-mystic-silver leading-relaxed" v-html="interpretationHtml" />
      </section>
    </template>
  </main>
</template>

<style scoped>
.cards-grid {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 2rem;
  max-width: 1000px;
}
.card-entry {
  display: flex;
  flex-direction: column;
  align-items: center;
}
.prose-mystic :deep(h2) {
  font-size: 1.1rem;
  font-weight: 600;
  color: #f5c26b;
  margin-top: 1.25rem;
  margin-bottom: 0.4rem;
  letter-spacing: 0.05em;
}
.prose-mystic :deep(strong) {
  color: #e8d5a3;
  font-weight: 600;
}
.prose-mystic :deep(ul) {
  list-style: disc;
  padding-left: 1.25rem;
  margin: 0.4rem 0;
}
.prose-mystic :deep(li) {
  margin-bottom: 0.2rem;
}
.prose-mystic :deep(p) {
  margin-bottom: 0.6rem;
}
</style>
