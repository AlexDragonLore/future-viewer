<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'

const auth = useAuthStore()
const route = useRoute()

const tabs = [
  { name: 'admin-feedbacks', label: 'Фидбеки', testId: 'admin-tab-feedbacks' },
  { name: 'admin-users', label: 'Пользователи', testId: 'admin-tab-users' },
  { name: 'admin-stats', label: 'Статистика', testId: 'admin-tab-stats' },
  { name: 'admin-tarot-plus', label: 'Таро+', testId: 'admin-tab-tarot-plus' },
]

const activeTab = computed(() => route.name?.toString() ?? '')
</script>

<template>
  <main class="admin-page min-h-screen px-4 sm:px-6 py-10 sm:py-12 max-w-6xl mx-auto" data-testid="admin-view">
    <header class="mb-6 text-center">
      <div class="admin-kicker text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ АДМИН ✦</div>
      <h1 class="admin-title font-display text-4xl gold-text">Панель администратора</h1>
      <p class="admin-email text-mystic-muted mt-2 text-sm">{{ auth.email }}</p>
    </header>

    <nav class="flex gap-2 justify-center mb-8 flex-wrap" data-testid="admin-tabs">
      <RouterLink
        v-for="tab in tabs"
        :key="tab.name"
        :to="{ name: tab.name }"
        class="admin-tab"
        :class="{ active: activeTab === tab.name }"
        :data-testid="tab.testId"
      >
        {{ tab.label }}
      </RouterLink>
    </nav>

    <RouterView />
  </main>
</template>

<style scoped>
.admin-tab {
  padding: 0.5rem 1.25rem;
  border-radius: 999px;
  font-family: 'Cinzel', serif;
  font-size: 0.8rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: rgba(224, 212, 186, 0.7);
  border: 1px solid rgba(245, 194, 107, 0.2);
  transition:
    color 0.2s ease,
    border-color 0.2s ease,
    background-color 0.2s ease;
}
.admin-tab:hover {
  color: rgba(245, 194, 107, 0.9);
  border-color: rgba(245, 194, 107, 0.5);
}
.admin-tab.active {
  background: rgba(245, 194, 107, 0.15);
  color: #f5c26b;
  border-color: rgba(245, 194, 107, 0.7);
}
@media (max-width: 640px) {
  .admin-page {
    padding-top: 2rem;
    padding-bottom: 2.5rem;
  }
  .admin-kicker {
    letter-spacing: 0.14em;
  }
  .admin-title {
    font-size: 2rem;
    line-height: 1.15;
  }
  .admin-email {
    overflow-wrap: anywhere;
  }
  .admin-tab {
    flex: 1 1 8rem;
    text-align: center;
    padding: 0.55rem 0.75rem;
    font-size: 0.72rem;
    letter-spacing: 0.08em;
  }
}
</style>
