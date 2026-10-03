<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { COOKIE_POLICY_VERSION, useCookiePreferences } from '@/composables/useCookiePreferences'

const cookiePreferences = useCookiePreferences()
const draft = reactive({ preferences: false, analytics: false, marketing: false })
const detailsOpen = ref(false)

watch(
  () => cookiePreferences.settingsOpen.value,
  (open) => {
    if (!open) return
    const current = cookiePreferences.preferences.value
    detailsOpen.value = current !== null
    draft.preferences = current?.preferences ?? false
    draft.analytics = current?.analytics ?? false
    draft.marketing = current?.marketing ?? false
  },
  { immediate: true },
)

cookiePreferences.loadCookiePreferences()

function saveDetailed() {
  cookiePreferences.saveDetailed({ ...draft })
}
</script>

<template>
  <div
    v-if="cookiePreferences.settingsOpen.value"
    class="cookie-layer"
    role="dialog"
    aria-modal="false"
    aria-labelledby="cookie-title"
    data-testid="cookie-preferences"
  >
    <section class="cookie-panel">
      <h2 id="cookie-title">Cookies и аналитика</h2>
      <p>
        Необходимое хранилище — для входа. Аналитика — только с вашего разрешения.
      </p>

      <div v-if="detailsOpen" class="cookie-details" data-testid="cookie-details">
        <p>Версия политики: {{ COOKIE_POLICY_VERSION }}.</p>
        <label>
          <input type="checkbox" checked disabled />
          <span><strong>Необходимые</strong> — вход, безопасность и сохранение этого выбора.</span>
        </label>
        <label>
          <input v-model="draft.preferences" type="checkbox" data-testid="cookie-preferences-toggle" />
          <span><strong>Настройки</strong> — например, выбранная колода.</span>
        </label>
        <label>
          <input v-model="draft.analytics" type="checkbox" data-testid="cookie-analytics-toggle" />
          <span><strong>Аналитические</strong> — Яндекс Метрика помогает понять, откуда приходят посетители и сколько регистраций и оплат приносит реклама.</span>
        </label>
        <label>
          <input v-model="draft.marketing" type="checkbox" data-testid="cookie-marketing-toggle" />
          <span><strong>Рекламные</strong> — сейчас сервис не использует рекламные скрипты.</span>
        </label>
        <button type="button" class="cookie-secondary" data-testid="save-cookie-details" @click="saveDetailed">
          Сохранить настройки
        </button>
      </div>

      <div class="cookie-actions">
        <button type="button" class="cookie-secondary" data-testid="accept-necessary" @click="cookiePreferences.acceptNecessary">
          Принять необходимые
        </button>
        <button type="button" class="glow-button" data-testid="accept-all" @click="cookiePreferences.acceptAll">
          Принять все
        </button>
      </div>
      <div class="cookie-links">
        <button type="button" class="details-toggle" :aria-expanded="detailsOpen" @click="detailsOpen = !detailsOpen">
          {{ detailsOpen ? 'Скрыть детали' : 'Детальная настройка' }}
        </button>
        <RouterLink to="/legal/cookies" class="cookie-link">Политика cookies</RouterLink>
      </div>
    </section>
  </div>
</template>

<style scoped>
.cookie-layer {
  position: fixed;
  z-index: 100;
  inset: auto 0 0;
  padding: 0.6rem;
  background: linear-gradient(0deg, rgba(4, 2, 10, 0.96), rgba(4, 2, 10, 0.16));
}
.cookie-panel {
  width: min(48rem, 100%);
  margin: 0 auto;
  border: 1px solid rgba(245, 194, 107, 0.42);
  border-radius: 1rem;
  background: #13082a;
  box-shadow: 0 1.5rem 4rem rgba(0, 0, 0, 0.52);
  max-height: calc(100dvh - 1.2rem);
  overflow-y: auto;
  padding: 0.8rem;
  color: rgba(224, 212, 186, 0.84);
}
.cookie-panel h2 {
  margin-bottom: 0.35rem;
  color: #f5c26b;
  font-size: 1rem;
}
.cookie-panel p,
.cookie-details {
  font-size: 0.78rem;
  line-height: 1.55;
}
.details-toggle,
.cookie-link {
  display: inline-block;
  color: #f5c26b;
  text-decoration: underline;
  text-underline-offset: 0.2em;
}
.cookie-links {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.4rem 0.7rem;
  margin-top: 0.45rem;
  font-size: 0.75rem;
}
.cookie-links > * {
  min-height: 1.75rem;
  display: inline-flex;
  align-items: center;
}
.cookie-details {
  display: grid;
  gap: 0.65rem;
  margin-top: 0.85rem;
  padding: 0.8rem;
  border-radius: 0.75rem;
  background: rgba(0, 0, 0, 0.24);
}
.cookie-details label {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
}
.cookie-details input {
  margin-top: 0.2rem;
  accent-color: #f5c26b;
}
.cookie-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  margin-top: 0.6rem;
}
.cookie-secondary {
  min-height: 2.75rem;
  border: 1px solid rgba(224, 212, 186, 0.3);
  border-radius: 999px;
  padding: 0.65rem 1rem;
  color: rgba(224, 212, 186, 0.9);
}
@media (max-width: 640px) {
  .cookie-actions > * {
    flex: 1;
    min-width: 0;
    min-height: 2.75rem;
    padding: 0.55rem 0.45rem;
    font-size: 0.75rem;
  }
}
</style>
