<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import {
  TAROT_SEO_CARDS,
  findTarotSeoCardBySlug,
} from '@/data/tarotSeoCatalog.js'
import { versionedCardImage } from '@/utils/assets'

const route = useRoute()

const slug = computed(() => String(route.params.slug ?? ''))
const card = computed(() => findTarotSeoCardBySlug(slug.value))
const relatedCards = computed(() =>
  (card.value?.relatedSlugs ?? [])
    .map((relatedSlug) => findTarotSeoCardBySlug(relatedSlug))
    .filter((relatedCard): relatedCard is NonNullable<typeof relatedCard> => Boolean(relatedCard)),
)
const suitCards = computed(() => {
  if (!card.value) return []
  return TAROT_SEO_CARDS
    .filter((candidate) => candidate.suitSlug === card.value?.suitSlug && candidate.slug !== card.value.slug)
    .slice(0, 6)
})
</script>

<template>
  <main class="seo-page min-h-screen px-4 sm:px-6 py-12 sm:py-16 max-w-6xl mx-auto">
    <nav class="breadcrumbs" aria-label="Навигация">
      <RouterLink to="/">Главная</RouterLink>
      <span>/</span>
      <RouterLink to="/glossary">Глоссарий</RouterLink>
      <span>/</span>
      <span>{{ card?.name ?? 'Карта Таро' }}</span>
    </nav>

    <section v-if="!card" class="not-found">
      <h1>Карта не найдена</h1>
      <p>Проверьте адрес или вернитесь к глоссарию Таро.</p>
      <RouterLink to="/glossary" class="seo-button">К глоссарию</RouterLink>
    </section>

    <article v-else>
      <header class="card-hero">
        <div class="hero-copy">
          <div class="seo-kicker">{{ card.group }} · {{ card.suitLabel }}</div>
          <h1>{{ card.name }}: значение карты Таро</h1>
          <p class="name-en">{{ card.nameEn }}</p>
          <p class="hero-summary">{{ card.summary }}.</p>
          <div class="keyword-row" aria-label="Ключевые значения">
            <span v-for="keyword in card.uprightKeywords.slice(0, 5)" :key="keyword">{{ keyword }}</span>
          </div>
        </div>
        <div class="hero-image">
          <img :src="versionedCardImage(card.imagePath)" :alt="`Карта Таро ${card.name}`" decoding="async" />
        </div>
      </header>

      <section class="content-grid" aria-label="Значения карты">
        <div class="content-panel">
          <h2>Прямое положение</h2>
          <p>{{ card.upright }}</p>
          <ul>
            <li v-for="keyword in card.uprightKeywords" :key="keyword">{{ keyword }}</li>
          </ul>
        </div>
        <div class="content-panel">
          <h2>Перевернутое положение</h2>
          <p>{{ card.reversed }}</p>
          <ul>
            <li v-for="keyword in card.reversedKeywords" :key="keyword">{{ keyword }}</li>
          </ul>
        </div>
      </section>

      <section class="meaning-section">
        <h2>Как читать {{ card.name }} в раскладе</h2>
        <p>
          В раскладе карта {{ card.name }} показывает тему: {{ card.summary }}. Ее точное значение зависит от позиции,
          соседних карт и вопроса. В вопросах о чувствах она чаще раскрывает внутренний мотив, в вопросах о работе -
          способ действия, а в вопросах выбора - цену следующего шага.
        </p>
        <p>
          Совет карты: {{ card.advice }} Если карта выпадает рядом с напряженными арканами, сначала ищите блок или
          скрытую причину. Если рядом поддерживающие карты, значение становится мягче и указывает на ресурс.
        </p>
        <p v-if="card.aliases.length" class="aliases">
          Также встречается как: {{ card.aliases.join(', ') }}.
        </p>
      </section>

      <section v-if="relatedCards.length" class="link-section">
        <h2>Связанные карты</h2>
        <div class="link-grid">
          <RouterLink
            v-for="related in relatedCards"
            :key="related.slug"
            :to="`/tarot/cards/${related.slug}`"
            class="seo-link-card"
          >
            <span>{{ related.name }}</span>
            <small>{{ related.summary }}</small>
          </RouterLink>
        </div>
      </section>

      <section v-if="suitCards.length" class="link-section">
        <h2>Другие карты раздела {{ card.suitLabel }}</h2>
        <div class="compact-links">
          <RouterLink v-for="item in suitCards" :key="item.slug" :to="`/tarot/cards/${item.slug}`">
            {{ item.name }}
          </RouterLink>
        </div>
      </section>

      <section class="cta-band">
        <div>
          <h2>Сделать расклад с этой картой</h2>
          <p>Задайте вопрос и получите AI-интерпретацию с учетом позиции карты, колоды и контекста расклада.</p>
        </div>
        <RouterLink to="/" class="seo-button">Начать расклад</RouterLink>
      </section>
    </article>
  </main>
</template>

<style scoped>
.seo-page {
  color: rgba(224, 212, 186, 0.9);
}
.breadcrumbs {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem;
  margin-bottom: 2rem;
  color: rgba(224, 212, 186, 0.54);
  font-size: 0.82rem;
}
.breadcrumbs a {
  color: #f5c26b;
  text-decoration: none;
}
.card-hero {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(220px, 320px);
  gap: 2rem;
  align-items: center;
  margin-bottom: 2.5rem;
}
.seo-kicker {
  color: #f5c26b;
  font-family: 'Cinzel', serif;
  font-size: 0.78rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  margin-bottom: 0.8rem;
}
h1,
h2 {
  font-family: 'Cinzel', serif;
  color: #f5c26b;
}
h1 {
  font-size: clamp(2.2rem, 6vw, 4.8rem);
  line-height: 1.05;
  margin: 0;
}
h2 {
  font-size: 1.45rem;
  margin-bottom: 0.75rem;
}
.name-en {
  margin-top: 0.8rem;
  color: rgba(224, 212, 186, 0.55);
  font-style: italic;
}
.hero-summary {
  max-width: 44rem;
  margin-top: 1rem;
  color: rgba(224, 212, 186, 0.78);
  font-size: 1.08rem;
  line-height: 1.75;
}
.hero-image img {
  width: 100%;
  border-radius: 10px;
  border: 1px solid rgba(245, 194, 107, 0.28);
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.38);
}
.keyword-row,
.compact-links {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
}
.keyword-row {
  margin-top: 1.2rem;
}
.keyword-row span,
.compact-links a {
  border: 1px solid rgba(245, 194, 107, 0.26);
  border-radius: 999px;
  padding: 0.35rem 0.75rem;
  color: rgba(245, 194, 107, 0.92);
  background: rgba(245, 194, 107, 0.06);
  text-decoration: none;
}
.content-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
  margin-bottom: 2rem;
}
.content-panel,
.meaning-section,
.seo-link-card,
.cta-band,
.not-found {
  border: 1px solid rgba(245, 194, 107, 0.18);
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.24);
}
.content-panel,
.meaning-section,
.not-found {
  padding: 1.25rem;
}
.content-panel p,
.meaning-section p,
.cta-band p,
.seo-link-card small,
.not-found p {
  color: rgba(224, 212, 186, 0.76);
  line-height: 1.75;
}
.content-panel ul {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem;
  list-style: none;
  padding: 0;
  margin: 1rem 0 0;
}
.content-panel li {
  color: rgba(245, 194, 107, 0.86);
  font-size: 0.85rem;
}
.meaning-section {
  margin-bottom: 2rem;
}
.aliases {
  color: rgba(245, 194, 107, 0.72);
}
.link-section {
  margin-bottom: 2rem;
}
.link-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 0.85rem;
}
.seo-link-card {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 1rem;
  text-decoration: none;
}
.seo-link-card span {
  color: #f5c26b;
  font-family: 'Cinzel', serif;
}
.cta-band {
  display: flex;
  gap: 1rem;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem;
}
.seo-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 2.8rem;
  padding: 0.7rem 1rem;
  border-radius: 8px;
  background: linear-gradient(135deg, #f5c26b, #d99a3d);
  color: #190f23;
  font-weight: 700;
  text-decoration: none;
  white-space: nowrap;
}
@media (max-width: 760px) {
  .card-hero,
  .content-grid,
  .cta-band {
    grid-template-columns: 1fr;
  }
  .card-hero {
    display: flex;
    flex-direction: column;
    align-items: stretch;
  }
  .hero-image {
    max-width: 220px;
    margin: 0 auto;
  }
  .cta-band {
    display: block;
  }
  .seo-button {
    width: 100%;
    margin-top: 1rem;
  }
}
</style>
