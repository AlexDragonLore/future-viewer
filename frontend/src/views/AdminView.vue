<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'

const auth = useAuthStore()
const route = useRoute()

const tabs = [
  { name: 'admin-readings', label: 'Сообщения', testId: 'admin-tab-readings' },
  { name: 'admin-feedbacks', label: 'Отзывы', testId: 'admin-tab-feedbacks' },
  { name: 'admin-users', label: 'Пользователи', testId: 'admin-tab-users' },
  { name: 'admin-stats', label: 'Статистика', testId: 'admin-tab-stats' },
]

const activeTab = computed(() => route.name === 'admin-user-detail' ? 'admin-users' : route.name?.toString() ?? '')
</script>

<template>
  <main class="admin-page w-full min-h-screen px-4 sm:px-6 py-6 sm:py-8 max-w-6xl mx-auto" data-testid="admin-view">
    <header class="admin-heading">
      <h1 class="admin-title">Панель администратора</h1>
      <p class="admin-email text-mystic-muted text-xs">{{ auth.email }}</p>
    </header>

    <nav class="flex gap-2 mb-4 flex-wrap" aria-label="Разделы администрирования" data-testid="admin-tabs">
      <RouterLink
        v-for="tab in tabs"
        :key="tab.name"
        :to="{ name: tab.name }"
        class="admin-tab"
        :class="{ active: activeTab === tab.name }"
        :aria-current="activeTab === tab.name ? 'page' : undefined"
        :data-testid="tab.testId"
      >
        {{ tab.label }}
      </RouterLink>
    </nav>

    <RouterView />
  </main>
</template>

<style scoped>
.admin-title { font-family: inherit; font-size: 1.25rem; font-weight: 600; letter-spacing: -0.01em; color: #ece3f1; }
.admin-heading { display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; gap: 0.35rem 1rem; margin-bottom: 0.75rem; }
.admin-tab {
  min-height: 44px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.5rem 1.25rem;
  border-radius: 9px;
  font-size: 0.8rem;
  letter-spacing: 0.01em;
  color: rgba(224, 212, 186, 0.7);
  border: 1px solid transparent;
  transition:
    color 0.2s ease,
    border-color 0.2s ease,
    background-color 0.2s ease;
}
.admin-email {
  overflow-wrap: anywhere;
}
.admin-tab:hover {
  color: rgba(245, 194, 107, 0.9);
  border-color: rgba(245, 194, 107, 0.5);
}
.admin-tab.active {
  background: rgba(202, 169, 118, 0.09);
  color: #f5c26b;
  border-color: rgba(202, 169, 118, 0.2);
}
@media (max-width: 640px) {
  .admin-page {
    padding-top: 1.25rem;
    padding-bottom: 2.5rem;
  }
  .admin-kicker {
    letter-spacing: 0.14em;
  }
  .admin-title {
    font-size: 1.3rem;
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
    letter-spacing: 0;
  }
}
</style>
