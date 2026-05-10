<script setup lang="ts">
import { merchant, paidProduct } from '@/content/legal'
</script>

<template>
  <main class="legal-page px-4 sm:px-6 py-10 sm:py-12">
    <section class="mystic-card legal-shell">
      <div class="text-mystic-accent text-xs tracking-[0.35em] mb-3 text-center">FUTURE VIEWER</div>
      <h1 class="font-display text-3xl md:text-4xl gold-text text-center mb-8">Оплата и условия сервиса</h1>

      <div class="legal-grid">
        <article class="legal-section">
          <h2>Товар или услуга</h2>
          <p>
            {{ paidProduct.title }} — {{ paidProduct.description }} Стоимость подписки:
            <strong>{{ paidProduct.price }} за {{ paidProduct.period }}</strong>.
          </p>
          <ul>
            <li v-for="item in paidProduct.included" :key="item">{{ item }}</li>
          </ul>
        </article>

        <article class="legal-section">
          <h2>Получение заказа</h2>
          <p>
            Future Viewer оказывает цифровую услугу. Физическая доставка не требуется. После успешной оплаты
            подписка активируется в аккаунте пользователя автоматически, а доступ к платным раскладам появляется
            в личном кабинете на сайте.
          </p>
          <p>
            Если платежная система передает подтверждение с задержкой, активация выполняется после получения
            статуса успешной оплаты. Пользователь может вернуться на сайт через страницу подтверждения платежа.
          </p>
        </article>

        <article class="legal-section">
          <h2>Порядок оплаты</h2>
          <p>
            Оплата принимается онлайн банковской картой или другим способом, доступным на платежной странице.
            Переход к оплате выполняется из интерфейса сервиса после регистрации или входа в аккаунт.
          </p>
          <p>
            Цена фиксирована и отображается до перехода к оплате: {{ paidProduct.price }} за {{ paidProduct.period }}.
          </p>
        </article>

        <article class="legal-section">
          <h2>Контакты и реквизиты</h2>
          <dl class="requisites">
            <div>
              <dt>Исполнитель</dt>
              <dd>{{ merchant.ownerName }}</dd>
            </div>
            <div>
              <dt>Статус</dt>
              <dd>{{ merchant.taxStatus }}</dd>
            </div>
            <div>
              <dt>ИНН</dt>
              <dd>{{ merchant.inn }}</dd>
            </div>
            <div>
              <dt>Телефон</dt>
              <dd>{{ merchant.phone }}</dd>
            </div>
            <div>
              <dt>Email</dt>
              <dd><a :href="`mailto:${merchant.email}`">{{ merchant.email }}</a></dd>
            </div>
            <div>
              <dt>Почтовый адрес</dt>
              <dd>{{ merchant.postalAddress }}</dd>
            </div>
          </dl>
        </article>
      </div>

      <article class="legal-section offer">
        <h2>Пользовательское соглашение и оферта</h2>
        <p>
          Настоящий документ является публичной офертой {{ merchant.ownerName }}, {{ merchant.taxStatus }},
          ИНН {{ merchant.inn }}, далее — Исполнитель, и определяет условия использования онлайн-сервиса
          {{ merchant.serviceName }}.
        </p>
        <p>
          Пользователь принимает условия оферты при регистрации, использовании сервиса или оплате подписки.
          Сервис предназначен для получения развлекательных и справочных интерпретаций Таро-раскладов.
          Материалы сервиса не являются медицинской, юридической, финансовой, психологической или иной
          профессиональной консультацией.
        </p>
        <p>
          Исполнитель предоставляет пользователю доступ к цифровой услуге после успешной оплаты. Пользователь
          обязуется указывать достоверные данные при регистрации, не нарушать работу сервиса и не использовать
          материалы сервиса незаконным способом.
        </p>
        <p>
          Возврат денежных средств рассматривается по обращению пользователя на электронную почту
          <a :href="`mailto:${merchant.email}`">{{ merchant.email }}</a>. Если услуга не была предоставлена по
          технической причине на стороне сервиса, Исполнитель либо восстанавливает доступ, либо возвращает оплату
          способом, доступным через платежного оператора.
        </p>
        <p>
          Исполнитель может обновлять условия оферты. Актуальная редакция публикуется на этой странице и действует
          с момента размещения, если в тексте обновления не указан иной срок.
        </p>
      </article>
    </section>
  </main>
</template>

<style scoped>
.legal-page {
  width: 100%;
  min-height: calc(100vh - 4rem);
}
.legal-shell {
  max-width: 68rem;
  margin: 0 auto;
  padding: 2rem;
}
.legal-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}
.legal-section {
  border: 1px solid rgba(245, 194, 107, 0.2);
  border-radius: 10px;
  background: rgba(0, 0, 0, 0.18);
  padding: 1.25rem;
  color: rgba(224, 212, 186, 0.84);
  line-height: 1.65;
}
.legal-section h2 {
  margin: 0 0 0.75rem;
  color: #f5c26b;
  font-family: 'Cinzel', serif;
  font-size: 1rem;
  letter-spacing: 0.08em;
}
.legal-section p {
  margin: 0 0 0.75rem;
}
.legal-section p:last-child {
  margin-bottom: 0;
}
.legal-section ul {
  margin: 0.75rem 0 0;
  padding-left: 1.1rem;
}
.legal-section li + li {
  margin-top: 0.35rem;
}
.offer {
  margin-top: 1rem;
}
.requisites {
  display: grid;
  gap: 0.65rem;
  margin: 0;
}
.requisites div {
  display: grid;
  grid-template-columns: 9rem minmax(0, 1fr);
  gap: 0.75rem;
}
.requisites dt {
  color: rgba(245, 194, 107, 0.72);
}
.requisites dd {
  margin: 0;
  overflow-wrap: anywhere;
}
a {
  color: #f5c26b;
  text-decoration: none;
}
a:hover {
  text-decoration: underline;
}
@media (max-width: 760px) {
  .legal-page {
    padding-top: 2rem;
    padding-bottom: 2.5rem;
  }
  .legal-shell {
    padding: 1rem;
  }
  .legal-shell > div:first-child {
    letter-spacing: 0.14em;
  }
  .legal-shell h1 {
    font-size: 1.85rem;
    line-height: 1.15;
  }
  .legal-section {
    padding: 1rem;
  }
  .legal-grid {
    grid-template-columns: 1fr;
  }
  .requisites div {
    grid-template-columns: 1fr;
    gap: 0.15rem;
  }
}
</style>
