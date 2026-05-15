<script setup lang="ts">
import { merchant } from '@/content/legal'
</script>

<template>
  <main class="privacy-page px-4 sm:px-6 py-10 sm:py-12">
    <section class="privacy-shell">
      <header class="privacy-hero">
        <div class="privacy-kicker">ВУАЛЬ ГРЯДУЩЕГО</div>
        <h1 class="font-display gold-text">Политика конфиденциальности</h1>
        <p>
          Эта страница описывает, какие данные нужны сервису, зачем они используются и как пользователь может
          связаться с исполнителем по вопросам обработки данных.
        </p>
      </header>

      <div class="privacy-grid">
        <article class="privacy-section">
          <h2>Аккаунт и профиль</h2>
          <p>
            Для регистрации используется email и пароль. После входа пользователь может указать имя, фамилию
            и дату рождения, чтобы расклады были персонализированы. Эти поля нужны для работы сервиса и могут
            быть обновлены пользователем в профиле.
          </p>
        </article>

        <article class="privacy-section">
          <h2>Вопросы и расклады</h2>
          <p>
            Сервис сохраняет вопросы, выбранные расклады, выпавшие карты, интерпретации и историю обращений,
            чтобы пользователь мог вернуться к результатам в личном кабинете.
          </p>
        </article>

        <article class="privacy-section">
          <h2>AI-интерпретации</h2>
          <p>
            Для генерации ответа данные расклада и вопрос пользователя передаются AI-провайдеру, настроенному
            в сервисе. Не передавайте в вопросах сведения, которые не хотите использовать для обработки ответа.
          </p>
        </article>

        <article class="privacy-section">
          <h2>Обратная связь</h2>
          <p>
            Отзывы о раскладах, оценки полезности и текст обратной связи используются для контроля качества
            интерпретаций, начисления баллов и работы достижений.
          </p>
        </article>

        <article class="privacy-section">
          <h2>Telegram</h2>
          <p>
            Если пользователь связывает Telegram, сервис хранит технический идентификатор чата и использует его
            для отправки уведомлений, например ссылок на обратную связь или сообщений о достижениях.
          </p>
        </article>

        <article class="privacy-section">
          <h2>Оплата</h2>
          <p>
            Платежи обрабатываются внешним платежным провайдером. Сервис хранит технический идентификатор оплаты,
            статус платежа и срок действия цифрового доступа. Данные банковской карты в приложении не хранятся.
          </p>
        </article>
      </div>

      <article class="privacy-contact">
        <h2>Контакт по вопросам данных</h2>
        <p>
          По вопросам доступа, уточнения или удаления данных можно написать исполнителю:
          <a :href="`mailto:${merchant.email}`">{{ merchant.email }}</a>.
        </p>
      </article>
    </section>
  </main>
</template>

<style scoped>
.privacy-page {
  width: 100%;
  min-height: calc(100vh - 4rem);
}
.privacy-shell {
  max-width: 68rem;
  margin: 0 auto;
}
.privacy-hero {
  max-width: 48rem;
  margin: 0 auto 2rem;
  text-align: center;
}
.privacy-kicker {
  color: #f5c26b;
  font-size: 0.72rem;
  letter-spacing: 0.32em;
  margin-bottom: 0.75rem;
}
.privacy-hero h1 {
  font-size: 2.6rem;
  line-height: 1.1;
  margin-bottom: 1rem;
}
.privacy-hero p,
.privacy-section p,
.privacy-contact p {
  color: rgba(224, 212, 186, 0.82);
  line-height: 1.7;
}
.privacy-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}
.privacy-section,
.privacy-contact {
  border: 1px solid rgba(245, 194, 107, 0.2);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.22);
  padding: 1.25rem;
}
.privacy-section h2,
.privacy-contact h2 {
  margin: 0 0 0.75rem;
  color: #f5c26b;
  font-family: 'Cinzel', serif;
  font-size: 1rem;
  letter-spacing: 0.08em;
}
.privacy-contact {
  margin-top: 1rem;
}
a {
  color: #f5c26b;
  text-decoration: none;
}
a:hover {
  text-decoration: underline;
}
@media (max-width: 760px) {
  .privacy-page {
    padding-top: 2rem;
    padding-bottom: 2.5rem;
  }
  .privacy-kicker {
    letter-spacing: 0.14em;
  }
  .privacy-hero h1 {
    font-size: 1.9rem;
  }
  .privacy-grid {
    grid-template-columns: 1fr;
  }
}
</style>
