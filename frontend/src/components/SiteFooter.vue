<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'
import { useCookiePreferences } from '@/composables/useCookiePreferences'

const { supportEmail } = storeToRefs(usePublicConfigStore())
const { openCookieSettings } = useCookiePreferences()
</script>

<template>
  <footer class="site-footer">
    <nav class="footer-row" aria-label="О сервисе">
      <RouterLink to="/about" class="footer-link">О сервисе</RouterLink>
      <RouterLink to="/faq" class="footer-link">Вопросы и ответы</RouterLink>
    </nav>

    <div class="footer-row footer-tools">
      <details class="footer-documents">
        <summary>Документы</summary>
        <nav class="footer-row document-links" aria-label="Правовые документы">
          <RouterLink to="/legal/offer" class="footer-link">Оферта</RouterLink>
          <RouterLink to="/legal/privacy" class="footer-link">Политика ПД</RouterLink>
          <RouterLink to="/legal/personal-data-consent" class="footer-link">Согласие</RouterLink>
          <RouterLink to="/legal/cookies" class="footer-link">Хранение в браузере</RouterLink>
          <RouterLink to="/legal/marketing-consent" class="footer-link">Реклама</RouterLink>
          <RouterLink to="/legal/ai-disclaimer" class="footer-link">ИИ и Таро</RouterLink>
          <RouterLink to="/legal/data-request" class="footer-link">Права на данные</RouterLink>
          <RouterLink to="/legal/processors" class="footer-link">Обработчики</RouterLink>
        </nav>
      </details>
      <button type="button" class="footer-link" data-testid="change-cookie-settings" @click="openCookieSettings">
        Настройки cookies
      </button>
    </div>

    <div class="footer-row footer-meta">
      <RouterLink to="/" class="footer-link footer-brand-link">Вуаль Грядущего</RouterLink>
      <a v-if="supportEmail" class="footer-link support-link" :href="`mailto:${supportEmail}`">{{ supportEmail }}</a>
    </div>
  </footer>
</template>

<style scoped>
.site-footer {
  position: relative;
  z-index: 15;
  width: 100%;
  margin-top: auto;
  padding: 0.6rem max(0.75rem, env(safe-area-inset-left)) max(0.6rem, env(safe-area-inset-bottom)) max(0.75rem, env(safe-area-inset-right));
  font-size: 0.8rem;
  line-height: 1.5;
  pointer-events: auto;
  overflow-wrap: anywhere;
  background: rgba(11, 6, 24, 0.74);
  border-top: 1px solid rgba(245, 194, 107, 0.1);
}
.footer-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  column-gap: 0.8rem;
}
.footer-link {
  display: inline-flex;
  min-height: 2.75rem;
  align-items: center;
  justify-content: center;
  color: #f5c26b;
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 0.25rem;
  text-align: center;
  text-decoration: underline;
  text-underline-offset: 0.2em;
  transition: color 0.2s ease;
  touch-action: manipulation;
}
.footer-link:hover,
.footer-documents summary:hover {
  color: #f8d98a;
}
.footer-documents summary {
  width: fit-content;
  min-height: 2.75rem;
  margin: 0 auto;
  padding: 0 0.25rem;
  line-height: 2.75rem;
  color: #f5c26b;
  cursor: pointer;
  touch-action: manipulation;
}
.footer-documents[open] {
  flex-basis: 100%;
}
.document-links {
  max-width: 48rem;
  margin: 0 auto;
  padding: 0.25rem 0 0.5rem;
}
.footer-meta {
  font-size: 0.75rem;
}
.footer-brand-link {
  color: rgba(224, 212, 186, 0.58);
  text-decoration: none;
}
.footer-link:focus-visible,
.footer-documents summary:focus-visible {
  outline: 2px solid #f5c26b;
  outline-offset: 2px;
  border-radius: 0.25rem;
}
</style>
