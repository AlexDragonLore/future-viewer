<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { findTarotSeoDeckBySlug } from '@/data/tarotSeoCatalog.js'

const route = useRoute()
const slug = computed(() => String(route.params.slug ?? ''))
const deck = computed(() => findTarotSeoDeckBySlug(slug.value))
</script>

<template>
  <main class="seo-page min-h-screen px-4 sm:px-6 py-12 sm:py-16 max-w-5xl mx-auto">
    <nav class="breadcrumbs" aria-label="Навигация">
      <RouterLink to="/">Главная</RouterLink>
      <span>/</span>
      <RouterLink to="/glossary">Глоссарий</RouterLink>
      <span>/</span>
      <span>{{ deck?.label ?? 'Колода Таро' }}</span>
    </nav>

    <section v-if="!deck" class="panel">
      <h1>Колода не найдена</h1>
      <p>Вернитесь к глоссарию и выберите одну из доступных школ Таро.</p>
      <RouterLink to="/glossary#decks" class="seo-button">К колодам</RouterLink>
    </section>

    <article v-else>
      <header class="hero">
        <div class="seo-kicker">Колода Таро · {{ deck.period }}</div>
        <h1>{{ deck.label }}</h1>
        <p>{{ deck.summary }}.</p>
      </header>

      <section class="panel">
        <h2>Символика и характер</h2>
        <p>{{ deck.description }}</p>
      </section>

      <section class="panel">
        <h2>Для каких вопросов подходит</h2>
        <div class="chips">
          <span v-for="item in deck.bestFor" :key="item">{{ item }}</span>
        </div>
        <p>
          Выбор колоды не меняет базовое значение карты, но меняет акцент: одна школа говорит строже, другая мягче,
          третья делает видимыми исторические или психологические слои вопроса.
        </p>
      </section>

      <section class="link-section">
        <h2>Связанные разделы</h2>
        <div class="link-grid">
          <RouterLink to="/tarot/spreads/karta-dnya" class="seo-link-card">
            <span>Карта дня</span>
            <small>Быстрый ежедневный фокус для выбранной колоды.</small>
          </RouterLink>
          <RouterLink to="/tarot/spreads/tri-karty" class="seo-link-card">
            <span>Три карты</span>
            <small>Динамика ситуации через прошлое, настоящее и вероятный вектор.</small>
          </RouterLink>
          <RouterLink to="/glossary" class="seo-link-card">
            <span>Глоссарий Таро</span>
            <small>Значения карт, колод и раскладов в одном справочнике.</small>
          </RouterLink>
        </div>
      </section>

      <section class="cta-band">
        <div>
          <h2>Сделать расклад</h2>
          <p>Выберите вопрос, расклад и колоду, чтобы получить связную AI-интерпретацию.</p>
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
.hero {
  max-width: 54rem;
  margin-bottom: 2rem;
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
  font-size: clamp(2.4rem, 7vw, 5rem);
  line-height: 1.05;
  margin: 0 0 1rem;
}
h2 {
  font-size: 1.45rem;
  margin-bottom: 0.75rem;
}
.hero p,
.panel p,
.cta-band p,
.seo-link-card small {
  color: rgba(224, 212, 186, 0.76);
  line-height: 1.75;
}
.panel,
.seo-link-card,
.cta-band {
  border: 1px solid rgba(245, 194, 107, 0.18);
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.24);
}
.panel,
.cta-band {
  padding: 1.25rem;
  margin-bottom: 2rem;
}
.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
  margin-bottom: 1rem;
}
.chips span {
  border: 1px solid rgba(245, 194, 107, 0.26);
  border-radius: 999px;
  padding: 0.35rem 0.75rem;
  color: rgba(245, 194, 107, 0.92);
  background: rgba(245, 194, 107, 0.06);
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
@media (max-width: 720px) {
  .cta-band {
    display: block;
  }
  .seo-button {
    width: 100%;
    margin-top: 1rem;
  }
}
</style>
