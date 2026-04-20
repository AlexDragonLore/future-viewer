<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useGlossaryStore } from '@/stores/useGlossaryStore'
import { CardSuit } from '@/types'
import { DECKS } from '@/data/decks'
import { SPREADS_META } from '@/data/spreads'

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
      <nav class="section-nav mt-6" aria-label="Разделы глоссария">
        <a href="#cards">Карты</a>
        <a href="#decks">Колоды</a>
        <a href="#spreads">Расклады</a>
      </nav>
    </header>

    <section id="decks" class="info-section">
      <h2 class="section-title">Колоды</h2>
      <p class="section-intro">Пять школ Таро, доступных в интерпретаторе. Выбор колоды влияет на визуальные акценты и тональность.</p>
      <ul class="info-grid" data-testid="decks-list">
        <li v-for="d in DECKS" :id="`deck-${d.anchorId}`" :key="d.value" class="info-card">
          <h3>{{ d.label }}</h3>
          <p>{{ d.longDescription }}</p>
        </li>
      </ul>
    </section>

    <section id="spreads" class="info-section">
      <h2 class="section-title">Расклады</h2>
      <p class="section-intro">Три формата вопросов — от экспресс-ответа до развёрнутой карты ситуации.</p>
      <ul class="info-grid" data-testid="spreads-list">
        <li v-for="s in SPREADS_META" :id="`spread-${s.anchorId}`" :key="s.type" class="info-card">
          <h3>{{ s.label }} <span class="muted">· {{ s.cardCount }} карт(ы)</span></h3>
          <p>{{ s.longDescription }}</p>
        </li>
      </ul>
    </section>

    <section id="cards" class="info-section">
      <h2 class="section-title">Карты</h2>
    </section>

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
.section-nav {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  justify-content: center;
}
.section-nav a {
  color: rgba(224, 212, 186, 0.75);
  text-decoration: none;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.14em;
  font-size: 0.72rem;
  text-transform: uppercase;
  padding: 0.3rem 0.75rem;
  border-radius: 999px;
  border: 1px solid rgba(245, 194, 107, 0.25);
  transition: all 0.2s ease;
}
.section-nav a:hover {
  color: #f5c26b;
  border-color: rgba(245, 194, 107, 0.6);
  background: rgba(245, 194, 107, 0.08);
}
.info-section {
  margin: 3rem 0 2.5rem;
  scroll-margin-top: 96px;
}
.section-title {
  font-family: 'Cinzel', serif;
  color: #f5c26b;
  font-size: 1.75rem;
  letter-spacing: 0.08em;
  margin-bottom: 0.5rem;
}
.section-intro {
  color: rgba(224, 212, 186, 0.7);
  margin-bottom: 1.5rem;
  max-width: 48rem;
}
.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 1rem;
  list-style: none;
  padding: 0;
  margin: 0;
}
.info-card {
  padding: 1.25rem;
  border: 1px solid rgba(245, 194, 107, 0.2);
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.25);
  scroll-margin-top: 96px;
}
.info-card h3 {
  font-family: 'Cinzel', serif;
  color: #f5c26b;
  font-size: 1.05rem;
  margin-bottom: 0.5rem;
  letter-spacing: 0.06em;
}
.info-card h3 .muted {
  color: rgba(224, 212, 186, 0.5);
  font-weight: normal;
  letter-spacing: 0;
}
.info-card p {
  color: rgba(224, 212, 186, 0.8);
  line-height: 1.6;
  font-size: 0.9rem;
}
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
