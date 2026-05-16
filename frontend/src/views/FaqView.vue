<script setup lang="ts">
import { FAQ_ITEMS } from '@/data/tarotSeoCatalog.js'
</script>

<template>
  <main class="faq-page min-h-screen px-4 sm:px-6 py-12 sm:py-16 max-w-5xl mx-auto">
    <nav class="breadcrumbs" aria-label="Навигация">
      <RouterLink to="/">Главная</RouterLink>
      <span>/</span>
      <span>FAQ</span>
    </nav>

    <header class="hero">
      <div class="seo-kicker">Вопросы о Таро</div>
      <h1>FAQ по онлайн-раскладам</h1>
      <p>
        Короткие ответы на практические вопросы: как формулировать запрос, как читать перевернутые карты и где границы
        AI-интерпретации.
      </p>
    </header>

    <section class="faq-list" aria-label="Частые вопросы">
      <article v-for="item in FAQ_ITEMS" :key="item.question" class="faq-item">
        <h2>{{ item.question }}</h2>
        <p>{{ item.answer }}</p>
      </article>
    </section>

    <section class="cta-band">
      <div>
        <h2>Перейти к практике</h2>
        <p>Выберите расклад, сформулируйте вопрос и получите связную интерпретацию карт.</p>
      </div>
      <RouterLink to="/" class="seo-button">Начать расклад</RouterLink>
    </section>
  </main>
</template>

<style scoped>
.faq-page {
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
  font-size: 1.2rem;
  margin-bottom: 0.7rem;
}
.hero p,
.faq-item p,
.cta-band p {
  color: rgba(224, 212, 186, 0.76);
  line-height: 1.75;
}
.faq-list {
  display: grid;
  gap: 0.9rem;
  margin-bottom: 2rem;
}
.faq-item,
.cta-band {
  border: 1px solid rgba(245, 194, 107, 0.18);
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.24);
  padding: 1.25rem;
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
