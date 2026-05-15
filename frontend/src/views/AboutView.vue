<template>
  <main class="trust-page px-4 sm:px-6 py-10 sm:py-12">
    <section class="trust-shell">
      <header class="trust-hero">
        <div class="trust-kicker">ВУАЛЬ ГРЯДУЩЕГО</div>
        <h1 class="font-display gold-text">О сервисе</h1>
        <p>
          Вуаль Грядущего помогает сформулировать вопрос, выбрать расклад Таро и получить структурированную
          AI-интерпретацию с сохранением результата в личном кабинете.
        </p>
      </header>

      <div class="trust-grid">
        <article class="trust-section">
          <h2>Что делает сервис</h2>
          <p>
            Пользователь выбирает расклад, задаёт вопрос и получает разбор карт: общий смысл, роли позиций,
            акценты по каждой карте и итоговую интерпретацию. Бесплатно доступна карта дня, расширенные расклады
            открываются через платный цифровой доступ.
          </p>
        </article>

        <article class="trust-section">
          <h2>Как используется AI</h2>
          <p>
            Интерпретации формируются автоматически на основе выбранного расклада, вопроса пользователя,
            выпавших карт и сохранённых настроек профиля. Ответы предназначены для самоанализа и развлечения,
            а не для медицинских, юридических, финансовых или иных профессиональных решений.
          </p>
        </article>

        <article class="trust-section">
          <h2>Прозрачность</h2>
          <p>
            На сайте опубликованы условия оплаты, реквизиты исполнителя и политика обработки данных.
            Платный доступ предоставляется как цифровая услуга без автосписаний: продление запускается
            пользователем вручную.
          </p>
        </article>

        <article class="trust-section">
          <h2>Обратная связь</h2>
          <p>
            После расклада сервис может предложить оставить отзыв о том, насколько интерпретация оказалась
            полезной. Такая обратная связь помогает оценивать качество ответов и улучшать пользовательский опыт.
          </p>
        </article>
      </div>

      <nav class="trust-links" aria-label="Документы сервиса">
        <RouterLink to="/privacy">Политика конфиденциальности</RouterLink>
        <RouterLink to="/legal">Оплата и оферта</RouterLink>
      </nav>
    </section>
  </main>
</template>

<style scoped>
.trust-page {
  width: 100%;
  min-height: calc(100vh - 4rem);
}
.trust-shell {
  max-width: 64rem;
  margin: 0 auto;
}
.trust-hero {
  max-width: 46rem;
  margin: 0 auto 2rem;
  text-align: center;
}
.trust-kicker {
  color: #f5c26b;
  font-size: 0.72rem;
  letter-spacing: 0.32em;
  margin-bottom: 0.75rem;
}
.trust-hero h1 {
  font-size: 2.75rem;
  line-height: 1.1;
  margin-bottom: 1rem;
}
.trust-hero p,
.trust-section p {
  color: rgba(224, 212, 186, 0.82);
  line-height: 1.7;
}
.trust-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}
.trust-section {
  border: 1px solid rgba(245, 194, 107, 0.2);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.22);
  padding: 1.25rem;
}
.trust-section h2 {
  margin: 0 0 0.75rem;
  color: #f5c26b;
  font-family: 'Cinzel', serif;
  font-size: 1rem;
  letter-spacing: 0.08em;
}
.trust-links {
  display: flex;
  justify-content: center;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-top: 1.5rem;
}
.trust-links a {
  color: #f5c26b;
  border: 1px solid rgba(245, 194, 107, 0.28);
  border-radius: 999px;
  padding: 0.5rem 0.9rem;
  text-decoration: none;
}
.trust-links a:hover {
  border-color: rgba(245, 194, 107, 0.65);
  background: rgba(245, 194, 107, 0.08);
}
@media (max-width: 760px) {
  .trust-page {
    padding-top: 2rem;
    padding-bottom: 2.5rem;
  }
  .trust-kicker {
    letter-spacing: 0.14em;
  }
  .trust-hero h1 {
    font-size: 2rem;
  }
  .trust-grid {
    grid-template-columns: 1fr;
  }
}
</style>
