<script setup lang="ts">
import { onMounted } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'
import AdminStatTile from '@/components/admin/AdminStatTile.vue'

const store = useAdminStore()

onMounted(() => store.loadStats())

function refresh(): void {
  store.loadStats()
}
</script>

<template>
  <section class="space-y-6" data-testid="admin-stats-view">
    <div class="flex justify-between items-center">
      <h2 class="font-display text-xl gold-text">Статистика</h2>
      <button
        class="admin-btn"
        :disabled="store.statsLoading"
        data-testid="admin-stats-refresh"
        @click="refresh"
      >
        {{ store.statsLoading ? 'Загрузка…' : 'Обновить' }}
      </button>
    </div>

    <div v-if="store.statsError" class="error" data-testid="admin-stats-error">
      {{ store.statsError }}
    </div>

    <div
      v-if="store.stats"
      class="grid gap-4 grid-cols-1 sm:grid-cols-2 lg:grid-cols-4"
      data-testid="admin-stats-grid"
    >
      <AdminStatTile
        label="Пользователей"
        :value="store.stats.totalUsers"
        test-id="admin-stat-total-users"
      />
      <AdminStatTile
        label="Админов"
        :value="store.stats.adminCount"
        test-id="admin-stat-admin-count"
      />
      <AdminStatTile
        label="Активных подписок"
        :value="store.stats.activeSubscriptions"
        test-id="admin-stat-active-subs"
      />
      <AdminStatTile
        label="Читок сегодня"
        :value="store.stats.readingsToday"
        test-id="admin-stat-readings-today"
      />
      <AdminStatTile
        label="Читок за неделю"
        :value="store.stats.readingsThisWeek"
        hint="последние 7 дней"
        test-id="admin-stat-readings-week"
      />
      <AdminStatTile
        label="Фидбеков к рассылке"
        :value="store.stats.pendingFeedbacksToNotify"
        hint="Pending, срок подошёл, с Telegram"
        test-id="admin-stat-pending-notify"
      />
      <AdminStatTile
        label="Оценено за месяц"
        :value="store.stats.scoredFeedbacksThisMonth"
        hint="последние 30 дней"
        test-id="admin-stat-scored-month"
      />
    </div>

    <div
      v-else-if="!store.statsLoading"
      class="empty"
      data-testid="admin-stats-empty"
    >
      Нажмите «Обновить», чтобы загрузить данные.
    </div>
  </section>
</template>

<style scoped>
.admin-btn {
  padding: 0.5rem 1.1rem;
  border: 1px solid rgba(245, 194, 107, 0.4);
  border-radius: 0.4rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.8rem;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}
.admin-btn:hover:not(:disabled) {
  background: rgba(245, 194, 107, 0.1);
  color: #f5c26b;
}
.admin-btn:disabled {
  opacity: 0.4;
}
.error {
  color: #ff8585;
  background: rgba(255, 50, 50, 0.08);
  border: 1px solid rgba(255, 50, 50, 0.2);
  padding: 0.6rem 0.9rem;
  border-radius: 0.4rem;
}
.empty {
  text-align: center;
  padding: 2rem;
  color: rgba(224, 212, 186, 0.6);
  font-style: italic;
}
</style>
