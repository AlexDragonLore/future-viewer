<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { findTarotSeoSpreadBySlug } from '@/data/tarotSeoCatalog.js'

const route = useRoute()
const slug = computed(() => String(route.params.slug ?? ''))
const spread = computed(() => findTarotSeoSpreadBySlug(slug.value))
</script>

<template>
  <main class="seo-page min-h-screen px-4 sm:px-6 py-12 sm:py-16 max-w-5xl mx-auto">
    <nav class="breadcrumbs" aria-label="Навигация">
      <RouterLink to="/">Главная</RouterLink>
      <span>/</span>
      <RouterLink to="/glossary">Глоссарий</RouterLink>
      <span>/</span>
      <span>{{ spread?.label ?? 'Расклад Таро' }}</span>
    </nav>

    <section v-if="!spread" class="panel">
      <h1>Расклад не найден</h1>
      <p>Вернитесь к глоссарию и выберите доступный формат расклада.</p>
      <RouterLink to="/glossary#spreads" class="seo-button">К раскладам</RouterLink>
    </section>

    <article v-else>
      <header class="hero">
        <div class="seo-kicker">Расклад Таро · {{ spread.cardCount }} карт</div>
        <h1>{{ spread.label }}</h1>
        <p>{{ spread.summary }}.</p>
      </header>

      <section class="panel intro">
        <h2>Когда использовать</h2>
        <p>{{ spread.description }}</p>
        <div class="chips">
          <span v-for="item in spread.bestFor" :key="item">{{ item }}</span>
        </div>
      </section>

      <section class="positions">
        <h2>Позиции карт</h2>
        <ol>
          <li v-for="(position, index) in spread.positions" :key="position">
            <span>{{ index + 1 }}</span>
            <strong>{{ position }}</strong>
          </li>
        </ol>
      </section>

      <section class="panel reading">
        <h2>Как читать результат</h2>
        <p>
          Сначала смотрите на общий тон карт: преобладают ли Старшие Арканы, какая масть повторяется чаще, есть ли
          напряженные или поддерживающие связки. Затем читайте каждую позицию отдельно и только после этого собирайте
          итоговую линию расклада.
        </p>
        <p>
          Для практики полезно записать вопрос перед началом. Так интерпретация не расплывается, а карты отвечают на
          конкретную тему, а не на тревогу вокруг нее.
        </p>
      </section>

      <section class="cta-band">
        <div>
          <h2>Попробовать {{ spread.label }}</h2>
          <p>Задайте вопрос и получите интерпретацию выбранного расклада с учетом колоды и контекста.</p>
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
  max-width: 52rem;
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
.cta-band p {
  color: rgba(224, 212, 186, 0.76);
  line-height: 1.75;
}
.panel,
.cta-band {
  border: 1px solid rgba(245, 194, 107, 0.18);
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.24);
  padding: 1.25rem;
  margin-bottom: 2rem;
}
.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
  margin-top: 1rem;
}
.chips span {
  border: 1px solid rgba(245, 194, 107, 0.26);
  border-radius: 999px;
  padding: 0.35rem 0.75rem;
  color: rgba(245, 194, 107, 0.92);
  background: rgba(245, 194, 107, 0.06);
}
.positions {
  margin-bottom: 2rem;
}
.positions ol {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
  gap: 0.85rem;
  list-style: none;
  padding: 0;
  margin: 0;
}
.positions li {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-height: 4rem;
  border: 1px solid rgba(245, 194, 107, 0.18);
  border-radius: 10px;
  background: rgba(0, 0, 0, 0.2);
  padding: 0.8rem;
}
.positions span {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  flex: 0 0 auto;
  border-radius: 999px;
  background: rgba(245, 194, 107, 0.14);
  color: #f5c26b;
  font-family: 'Cinzel', serif;
}
.positions strong {
  color: rgba(224, 212, 186, 0.88);
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
