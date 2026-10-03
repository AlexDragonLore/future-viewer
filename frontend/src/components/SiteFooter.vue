<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'
import { useCookiePreferences } from '@/composables/useCookiePreferences'

const { supportEmail } = storeToRefs(usePublicConfigStore())
const { openCookieSettings } = useCookiePreferences()
</script>

<template>
  <footer class="site-footer">
    <RouterLink to="/about" class="about-link">О сервисе</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/legal/offer" class="about-link">Оферта</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/legal/privacy" class="about-link">Политика ПД</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/legal/personal-data-consent" class="about-link">Согласие</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/legal/cookies" class="about-link">Хранение в браузере</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/legal/marketing-consent" class="about-link">Реклама</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/legal/ai-disclaimer" class="about-link">ИИ и Таро</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/legal/data-request" class="about-link">Права на данные</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/legal/processors" class="about-link">Обработчики</RouterLink>
    <span class="footer-separator">·</span>
    <button type="button" class="about-link" data-testid="change-cookie-settings" @click="openCookieSettings">
      Настройки cookies
    </button>
    <span class="footer-separator">·</span>
    <RouterLink to="/faq" class="about-link">Вопросы и ответы</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/" class="footer-brand-link">Вуаль Грядущего</RouterLink>
    <template v-if="supportEmail">
      <span class="footer-separator">·</span>
      <span class="footer-muted footer-support">
        Связаться с нами:
        <a class="support-link" :href="`mailto:${supportEmail}`">{{ supportEmail }}</a>
      </span>
    </template>
  </footer>
</template>

<style scoped>
.site-footer {
  position: relative;
  z-index: 15;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: 0.35rem 0.6rem;
  width: 100%;
  margin-top: auto;
  padding: 1rem max(1rem, env(safe-area-inset-left)) max(1rem, env(safe-area-inset-bottom)) max(1rem, env(safe-area-inset-right));
  font-size: 0.8rem;
  line-height: 1.5;
  letter-spacing: 0.04em;
  pointer-events: auto;
  overflow-wrap: anywhere;
  background: rgba(11, 6, 24, 0.74);
  border-top: 1px solid rgba(245, 194, 107, 0.1);
}
.footer-separator {
  color: rgba(224, 212, 186, 0.3);
}
.footer-muted {
  min-width: 0;
  color: rgba(224, 212, 186, 0.42);
}
.footer-brand-link {
  display: inline-flex;
  min-height: 2.75rem;
  align-items: center;
  justify-content: center;
  min-width: 0;
  color: rgba(224, 212, 186, 0.58);
  text-decoration: none;
  transition:
    color 0.3s ease,
    text-shadow 0.3s ease;
  touch-action: manipulation;
}
.footer-brand-link:hover {
  color: rgba(245, 194, 107, 0.92);
  text-shadow: 0 0 10px rgba(245, 194, 107, 0.45);
}
.footer-support {
  display: inline-flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.25rem;
}
.about-link {
  display: inline-flex;
  min-height: 2.75rem;
  align-items: center;
  justify-content: center;
  color: #f5c26b;
  background: transparent;
  border: none;
  cursor: pointer;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  font-size: 0.75rem;
  padding: 0 0.45rem;
  transition: text-shadow 0.3s ease;
  white-space: nowrap;
  touch-action: manipulation;
}
.about-link:hover {
  text-shadow: 0 0 12px rgba(245, 194, 107, 0.8);
}
.support-link {
  display: inline-flex;
  min-height: 2.5rem;
  align-items: center;
  justify-content: center;
  color: #f5c26b;
  text-decoration: none;
  touch-action: manipulation;
  transition: text-shadow 0.3s ease;
}
.support-link:hover {
  text-shadow: 0 0 10px rgba(245, 194, 107, 0.7);
}
@media (max-width: 640px) {
  .site-footer {
    flex-direction: column;
    gap: 0.4rem;
    padding: 1rem 0.75rem calc(1.25rem + env(safe-area-inset-bottom));
    font-size: 0.72rem;
  }
  .footer-separator {
    display: none;
  }
  .about-link {
    width: min(100%, 22rem);
    border: 1px solid rgba(245, 194, 107, 0.22);
    border-radius: 999px;
    background: rgba(245, 194, 107, 0.05);
    font-size: 0.68rem;
  }
  .footer-brand-link {
    width: min(100%, 22rem);
  }
  .support-link {
    min-height: 2.75rem;
    padding: 0 0.45rem;
  }
}
</style>
