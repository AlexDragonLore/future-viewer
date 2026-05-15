<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'

const { supportEmail } = storeToRefs(usePublicConfigStore())
</script>

<template>
  <footer class="site-footer">
    <RouterLink to="/about" class="about-link">О сервисе</RouterLink>
    <span class="mx-2 text-mystic-silver/30">·</span>
    <RouterLink to="/legal" class="about-link">Оплата и оферта</RouterLink>
    <span class="mx-2 text-mystic-silver/30">·</span>
    <RouterLink to="/privacy" class="about-link">Конфиденциальность</RouterLink>
    <span class="mx-2 text-mystic-silver/30">·</span>
    <span class="text-mystic-silver/40">Вуаль Грядущего</span>
    <template v-if="supportEmail">
      <span class="mx-2 text-mystic-silver/30">·</span>
      <span class="text-mystic-silver/40">
        Связаться с нами:
        <a class="support-link" :href="`mailto:${supportEmail}`">{{ supportEmail }}</a>
      </span>
    </template>
  </footer>
</template>

<style scoped>
.site-footer {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 15;
  text-align: center;
  padding: 0.75rem 1rem;
  font-size: 0.8rem;
  letter-spacing: 0.1em;
  pointer-events: auto;
  overflow-wrap: anywhere;
}
.about-link {
  color: #f5c26b;
  background: transparent;
  border: none;
  cursor: pointer;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  transition: text-shadow 0.3s ease;
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
    position: static;
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: 0.25rem 0.35rem;
    padding: 1rem 0.75rem 1.25rem;
    font-size: 0.72rem;
    letter-spacing: 0.04em;
    background: rgba(11, 6, 24, 0.72);
    border-top: 1px solid rgba(245, 194, 107, 0.12);
  }
  .about-link {
    font-size: 0.68rem;
    letter-spacing: 0.08em;
    padding: 0.2rem 0.25rem;
  }
}
</style>
