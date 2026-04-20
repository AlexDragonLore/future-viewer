<script setup lang="ts">
import { useAdminStore } from '@/stores/useAdminStore'
import { SubscriptionStatusValue } from '@/types'
import type { AdminUserListItem } from '@/types/admin'

const store = useAdminStore()
const emit = defineEmits<{ select: [id: string] }>()

function statusLabel(s: SubscriptionStatusValue): string {
  switch (s) {
    case SubscriptionStatusValue.Active: return 'Active'
    case SubscriptionStatusValue.Expired: return 'Expired'
    case SubscriptionStatusValue.Cancelled: return 'Cancelled'
    default: return 'None'
  }
}

function onRow(u: AdminUserListItem): void {
  emit('select', u.id)
}
</script>

<template>
  <div v-if="store.userLoading" class="empty" data-testid="admin-users-loading">Загрузка…</div>
  <div v-else-if="store.users.length === 0" class="empty" data-testid="admin-users-empty">
    Нет пользователей
  </div>
  <table v-else class="admin-table" data-testid="admin-users-table">
    <thead>
      <tr>
        <th>Email</th>
        <th>Создан</th>
        <th>Админ</th>
        <th>Подписка</th>
        <th>Читок</th>
        <th>Фидбеков</th>
        <th>Баллов</th>
        <th>TG</th>
      </tr>
    </thead>
    <tbody>
      <tr
        v-for="u in store.users"
        :key="u.id"
        data-testid="admin-user-row"
        class="cursor-pointer"
        @click="onRow(u)"
      >
        <td>{{ u.email }}</td>
        <td class="mono">{{ new Date(u.createdAt).toLocaleDateString() }}</td>
        <td>
          <span v-if="u.isAdmin" class="badge admin">admin</span>
          <span v-else class="badge muted">—</span>
        </td>
        <td>
          <span class="badge" :class="{ active: u.subscriptionStatus === SubscriptionStatusValue.Active }">
            {{ statusLabel(u.subscriptionStatus) }}
          </span>
          <span v-if="u.subscriptionExpiresAt" class="expiry">
            → {{ new Date(u.subscriptionExpiresAt).toLocaleDateString() }}
          </span>
        </td>
        <td class="num">{{ u.totalReadings }}</td>
        <td class="num">{{ u.totalFeedbacks }}</td>
        <td class="num">{{ u.totalScore }}</td>
        <td>
          <span v-if="u.telegramChatId" class="badge active">✓</span>
          <span v-else class="badge muted">—</span>
        </td>
      </tr>
    </tbody>
  </table>
</template>

<style scoped>
.empty {
  text-align: center;
  padding: 2rem;
  color: rgba(224, 212, 186, 0.6);
  font-style: italic;
}
.admin-table {
  width: 100%;
  border-collapse: collapse;
  font-family: 'Inter', system-ui, sans-serif;
}
.admin-table thead th {
  padding: 0.65rem 0.5rem;
  text-align: left;
  font-family: 'Cinzel', serif;
  font-size: 0.7rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: rgba(245, 194, 107, 0.8);
  border-bottom: 1px solid rgba(245, 194, 107, 0.25);
}
.admin-table tbody tr {
  border-bottom: 1px solid rgba(245, 194, 107, 0.08);
}
.admin-table tbody tr:hover {
  background: rgba(245, 194, 107, 0.06);
}
.admin-table td {
  padding: 0.5rem 0.5rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.85rem;
  vertical-align: middle;
}
.mono {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.78rem;
}
.num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
.badge {
  display: inline-block;
  padding: 0.1rem 0.5rem;
  border-radius: 999px;
  font-size: 0.7rem;
  letter-spacing: 0.05em;
  border: 1px solid rgba(245, 194, 107, 0.25);
  color: rgba(224, 212, 186, 0.8);
}
.badge.admin {
  background: rgba(245, 194, 107, 0.2);
  color: #f5c26b;
  border-color: rgba(245, 194, 107, 0.6);
}
.badge.active {
  background: rgba(80, 200, 120, 0.15);
  color: #98e09d;
  border-color: rgba(80, 200, 120, 0.4);
}
.badge.muted {
  opacity: 0.5;
}
.expiry {
  margin-left: 0.5rem;
  font-size: 0.75rem;
  color: rgba(224, 212, 186, 0.6);
}
</style>
