<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'

const { supportEmail } = storeToRefs(usePublicConfigStore())
</script>

<template>
  <footer class="site-footer">
    <RouterLink to="/about" class="about-link">О сервисе</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/legal" class="about-link">Оплата и оферта</RouterLink>
    <span class="footer-separator">·</span>
    <RouterLink to="/privacy" class="about-link">Конфиденциальность</RouterLink>
    <span class="footer-separator">·</span>
    <span class="footer-muted">Вуаль Грядущего</span>
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
.footer-support {
  display: inline-flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.25rem;
}
.about-link {
  color: #f5c26b;
  background: transparent;
  border: none;
  cursor: pointer;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  font-size: 0.75rem;
  padding: 0.2rem 0.25rem;
  transition: text-shadow 0.3s ease;
  white-space: nowrap;
}
.about-link:hover {
  text-shadow: 0 0 12px rgba(245, 194, 107, 0.8);
}
.support-link {
  color: #f5c26b;
  text-decoration: none;
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
    font-size: 0.68rem;
  }
}
</style>
