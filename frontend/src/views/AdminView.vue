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
]

const activeTab = computed(() => route.name?.toString() ?? '')
</script>

<template>
  <main class="min-h-screen px-6 py-12 max-w-6xl mx-auto" data-testid="admin-view">
    <header class="mb-6 text-center">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ АДМИН ✦</div>
      <h1 class="font-display text-4xl gold-text">Панель администратора</h1>
      <p class="text-mystic-muted mt-2 text-sm">{{ auth.email }}</p>
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
  transition: all 0.2s ease;
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
</style>
