<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useAchievementStore } from '@/stores/useAchievementStore'
import AchievementCard from '@/components/AchievementCard.vue'

const store = useAchievementStore()

const unlockedCount = computed(() => store.items.filter((a) => a.unlockedAt != null).length)

onMounted(async () => {
  await store.loadMine()
})
</script>

<template>
  <main class="min-h-screen px-6 py-16 max-w-5xl mx-auto">
    <header class="mb-8 text-center">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ АЧИВКИ ✦</div>
      <h1 class="font-display text-4xl gold-text">Достижения</h1>
      <p v-if="!store.loading && !store.error" class="text-mystic-silver/70 mt-2" data-testid="achievements-counter">
        Открыто {{ unlockedCount }} из {{ store.items.length }}
      </p>
    </header>

    <div v-if="store.loading" class="text-center text-mystic-silver/60" data-testid="achievements-loading">
      загружаю…
    </div>
    <div v-else-if="store.error" class="text-center text-red-300" data-testid="achievements-error">
      {{ store.error }}
    </div>
    <div
      v-else-if="store.items.length === 0"
      class="text-center text-mystic-silver/60"
      data-testid="achievements-empty"
    >
      Пока нет доступных ачивок.
    </div>
    <section v-else class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4" data-testid="achievements-grid">
      <AchievementCard v-for="a in store.items" :key="a.id" :achievement="a" />
    </section>
  </main>
</template>
