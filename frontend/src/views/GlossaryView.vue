<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useGlossaryStore } from '@/stores/useGlossaryStore'
import { CardSuit } from '@/types'

const store = useGlossaryStore()

type Filter = 'all' | CardSuit
const filter = ref<Filter>('all')

const suitOptions: { value: Filter; label: string }[] = [
  { value: 'all', label: 'Все' },
  { value: CardSuit.MajorArcana, label: 'Старшие' },
  { value: CardSuit.Wands, label: 'Жезлы' },
  { value: CardSuit.Cups, label: 'Кубки' },
  { value: CardSuit.Swords, label: 'Мечи' },
  { value: CardSuit.Pentacles, label: 'Пентакли' },
]

const filtered = computed(() => {
  if (filter.value === 'all') return store.cards
  return store.cards.filter((c) => c.suit === filter.value)
})

onMounted(() => store.loadAll())
</script>

<template>
  <main class="min-h-screen px-6 py-16 max-w-6xl mx-auto">
    <header class="mb-10 text-center">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ ГЛОССАРИЙ ✦</div>
      <h1 class="font-display text-4xl md:text-5xl gold-text mb-3">78 Карт Таро</h1>
      <p class="text-mystic-silver/70 max-w-xl mx-auto">
        Значения, ключевые слова и вариации по колодам.
      </p>
    </header>

    <nav class="flex flex-wrap gap-2 justify-center mb-8">
      <button
        v-for="opt in suitOptions"
        :key="String(opt.value)"
        class="suit-chip"
        :class="{ active: filter === opt.value }"
        @click="filter = opt.value"
      >
        {{ opt.label }}
      </button>
    </nav>

    <div v-if="store.loading" class="text-center text-mystic-silver/60">загружаю…</div>
    <div v-else-if="store.error" class="text-center text-red-300">{{ store.error }}</div>
    <div v-else-if="filtered.length === 0" class="text-center text-mystic-silver/60">
      Ничего не найдено.
    </div>

    <ul v-else class="card-grid">
      <li v-for="card in filtered" :key="card.id">
        <RouterLink :to="{ name: 'glossary-card', params: { id: card.id } }" class="card-tile">
          <img
            v-if="card.imagePath"
            :src="card.imagePath"
            :alt="card.name"
            loading="lazy"
            class="card-image"
          />
          <div v-else class="card-placeholder">✦</div>
          <div class="card-title">{{ card.name }}</div>
          <div class="card-sub">{{ card.nameEn }}</div>
        </RouterLink>
      </li>
    </ul>
  </main>
</template>

<style scoped>
.suit-chip {
  padding: 0.45rem 1rem;
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 999px;
  font-size: 0.85rem;
  background: rgba(0, 0, 0, 0.2);
  color: inherit;
  cursor: pointer;
  transition: all 0.25s ease;
}
.suit-chip:hover {
  border-color: rgba(245, 194, 107, 0.6);
  background: rgba(245, 194, 107, 0.08);
}
.suit-chip.active {
  border-color: #f5c26b;
  background: rgba(245, 194, 107, 0.18);
  color: #f5c26b;
}
.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 1.25rem;
}
.card-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.75rem;
  border: 1px solid rgba(245, 194, 107, 0.2);
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.25);
  text-decoration: none;
  transition: all 0.25s ease;
}
.card-tile:hover {
  border-color: rgba(245, 194, 107, 0.6);
  background: rgba(245, 194, 107, 0.08);
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(245, 194, 107, 0.15);
}
.card-image {
  width: 100%;
  aspect-ratio: 2 / 3;
  object-fit: cover;
  border-radius: 8px;
  margin-bottom: 0.5rem;
}
.card-placeholder {
  width: 100%;
  aspect-ratio: 2 / 3;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2rem;
  color: rgba(245, 194, 107, 0.4);
  border-radius: 8px;
  background: rgba(245, 194, 107, 0.05);
  margin-bottom: 0.5rem;
}
.card-title {
  font-family: var(--font-display, 'Cinzel', serif);
  color: #f5c26b;
  font-size: 0.95rem;
  text-align: center;
}
.card-sub {
  font-size: 0.7rem;
  color: rgba(229, 217, 188, 0.55);
  text-align: center;
  margin-top: 0.15rem;
}
</style>
