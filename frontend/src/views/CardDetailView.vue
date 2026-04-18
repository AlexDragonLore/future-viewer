<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useGlossaryStore } from '@/stores/useGlossaryStore'
import { DeckType } from '@/types'

const route = useRoute()
const store = useGlossaryStore()

const deckLabels: Record<DeckType, string> = {
  [DeckType.RWS]: 'Райдер–Уэйт',
  [DeckType.Thoth]: 'Тота',
  [DeckType.Marseille]: 'Марсельская',
  [DeckType.ViscontiSforza]: 'Висконти-Сфорца',
  [DeckType.ModernWitch]: 'Modern Witch',
}

const activeDeck = ref<DeckType>(DeckType.RWS)

const card = computed(() => store.detail)

watch(
  () => route.params.id,
  (id) => {
    const parsed = Number(id)
    if (!Number.isFinite(parsed)) return
    store.loadDetail(parsed)
  },
  { immediate: true },
)

watch(card, (c) => {
  if (c && c.deckVariants.length > 0) {
    activeDeck.value = c.deckVariants[0].deckType
  }
})

function variantNoteFor(deck: DeckType): string | null {
  if (!card.value) return null
  const v = card.value.deckVariants.find((x) => x.deckType === deck)
  return v?.variantNote ?? null
}
</script>

<template>
  <main class="min-h-screen px-6 py-16 max-w-4xl mx-auto">
    <div class="mb-6">
      <RouterLink to="/glossary" class="text-sm text-mystic-accent/80 hover:text-mystic-accent transition">
        ← К глоссарию
      </RouterLink>
    </div>

    <div v-if="store.loading" class="text-center text-mystic-silver/60">загружаю…</div>
    <div v-else-if="store.error" class="text-center text-red-300">{{ store.error }}</div>
    <div v-else-if="!card" class="text-center text-mystic-silver/60">Карта не найдена.</div>

    <article v-else class="flex flex-col md:flex-row gap-8">
      <div class="md:w-1/3 flex-shrink-0">
        <img
          v-if="card.imagePath"
          :src="card.imagePath"
          :alt="card.name"
          class="w-full rounded-xl border border-mystic-accent/30"
        />
        <div v-else class="card-placeholder">✦</div>
      </div>

      <div class="flex-1">
        <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ КАРТА ✦</div>
        <h1 class="font-display text-4xl gold-text mb-1">{{ card.name }}</h1>
        <div class="text-mystic-silver/60 mb-6 italic">{{ card.nameEn }}</div>

        <section class="mystic-card p-5 mb-4">
          <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">Прямое положение</div>
          <p class="text-mystic-silver/90 mb-3">{{ card.descriptionUpright }}</p>
          <div v-if="card.uprightKeywords.length" class="flex flex-wrap gap-2">
            <span v-for="kw in card.uprightKeywords" :key="kw" class="keyword-chip">{{ kw }}</span>
          </div>
        </section>

        <section class="mystic-card p-5 mb-4">
          <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">Перевёрнутое положение</div>
          <p class="text-mystic-silver/90 mb-3">{{ card.descriptionReversed }}</p>
          <div v-if="card.reversedKeywords.length" class="flex flex-wrap gap-2">
            <span v-for="kw in card.reversedKeywords" :key="kw" class="keyword-chip">{{ kw }}</span>
          </div>
        </section>

        <section v-if="card.aliases.length" class="mystic-card p-5 mb-4">
          <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">Синонимы</div>
          <p class="text-mystic-silver/80 text-sm">{{ card.aliases.join(' · ') }}</p>
        </section>

        <section v-if="card.deckVariants.length" class="mystic-card p-5">
          <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Вариации по колодам</div>
          <div class="deck-tabs">
            <button
              v-for="variant in card.deckVariants"
              :key="variant.deckType"
              class="deck-tab"
              :class="{ active: activeDeck === variant.deckType }"
              @click="activeDeck = variant.deckType"
            >
              {{ deckLabels[variant.deckType] }}
            </button>
          </div>
          <p class="text-mystic-silver/90 mt-3 text-sm leading-relaxed">
            {{ variantNoteFor(activeDeck) }}
          </p>
        </section>
      </div>
    </article>
  </main>
</template>

<style scoped>
.card-placeholder {
  width: 100%;
  aspect-ratio: 2 / 3;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 3rem;
  color: rgba(245, 194, 107, 0.4);
  border-radius: 12px;
  background: rgba(245, 194, 107, 0.05);
  border: 1px solid rgba(245, 194, 107, 0.2);
}
.keyword-chip {
  padding: 0.25rem 0.7rem;
  font-size: 0.75rem;
  border: 1px solid rgba(245, 194, 107, 0.3);
  border-radius: 999px;
  color: rgba(245, 194, 107, 0.85);
  background: rgba(245, 194, 107, 0.05);
}
.deck-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}
.deck-tab {
  padding: 0.35rem 0.8rem;
  font-size: 0.8rem;
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.2);
  color: inherit;
  cursor: pointer;
  transition: all 0.25s ease;
}
.deck-tab:hover {
  border-color: rgba(245, 194, 107, 0.55);
  background: rgba(245, 194, 107, 0.08);
}
.deck-tab.active {
  border-color: #f5c26b;
  background: rgba(245, 194, 107, 0.18);
  color: #f5c26b;
}
</style>
