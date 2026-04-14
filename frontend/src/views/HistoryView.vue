<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { readingApi } from '@/api/readingApi'
import type { Reading } from '@/types'

const readings = ref<Reading[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

onMounted(async () => {
  try {
    readings.value = await readingApi.history()
  } catch (e: any) {
    error.value = e.response?.data?.message ?? e.message
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <main class="min-h-screen px-6 py-16 max-w-3xl mx-auto">
    <header class="mb-8 text-center">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ АРХИВ ✦</div>
      <h1 class="font-display text-4xl gold-text">История</h1>
    </header>

    <div v-if="loading" class="text-center text-mystic-silver/60">загружаю…</div>
    <div v-else-if="error" class="text-center text-red-300">{{ error }}</div>
    <div v-else-if="readings.length === 0" class="text-center text-mystic-silver/60">
      Пока что пусто. Сделай первый расклад.
    </div>

    <ul v-else class="space-y-4">
      <li v-for="r in readings" :key="r.id" class="mystic-card p-5">
        <div class="flex justify-between items-start mb-2">
          <div class="font-display text-mystic-accent">{{ r.spreadName }}</div>
          <div class="text-xs text-mystic-silver/50">{{ new Date(r.createdAt).toLocaleString() }}</div>
        </div>
        <p class="italic text-mystic-silver/70 mb-2">«{{ r.question }}»</p>
        <p class="text-sm text-mystic-silver/90 line-clamp-3">{{ r.interpretation }}</p>
      </li>
    </ul>

    <div class="text-center mt-8">
      <RouterLink to="/" class="glow-button inline-block">Новый расклад</RouterLink>
    </div>
  </main>
</template>
