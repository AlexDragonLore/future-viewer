<script setup lang="ts">
import { useAdminStore } from '@/stores/useAdminStore'
import { SubscriptionStatusValue } from '@/types'
import type { AdminUserListItem } from '@/types/admin'

const store = useAdminStore()
const emit = defineEmits<{ select: [id: string] }>()

function statusLabel(s: SubscriptionStatusValue): string {
  switch (s) {
    case SubscriptionStatusValue.Active: return 'Активен'
    case SubscriptionStatusValue.Expired: return 'Истёк'
    case SubscriptionStatusValue.Cancelled: return 'Отменён'
    default: return 'Бесплатный'
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
  <template v-else>
    <div class="table-scroll">
      <table class="admin-table" data-testid="admin-users-table">
        <thead>
          <tr>
            <th>Email</th>
            <th class="mobile-hide">Регистрация</th>
            <th class="mobile-hide">Админ</th>
            <th>Доступ</th>
            <th class="num">Раскладов</th>
            <th class="num mobile-hide">Отзывов</th>
            <th class="num">Баллов</th>
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
            <td><button type="button" class="user-link" @click.stop="onRow(u)">{{ u.email }}</button></td>
            <td class="mono mobile-hide">{{ new Date(u.createdAt).toLocaleDateString() }}</td>
            <td class="mobile-hide">
              <span v-if="u.isAdmin" class="badge admin">Админ</span>
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
            <td class="num mobile-hide">{{ u.totalFeedbacks }}</td>
            <td class="num">{{ u.totalScore }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <ul class="admin-user-cards" data-testid="admin-users-mobile-list">
      <li
        v-for="u in store.users"
        :key="`mobile-${u.id}`"
      >
        <button type="button" class="admin-user-card" data-testid="admin-user-card-button" @click="onRow(u)">
        <div class="mobile-card-head">
          <strong>{{ u.email }}</strong>
          <span v-if="u.isAdmin" class="badge admin">Админ</span>
        </div>
        <div class="mobile-card-meta mono">{{ new Date(u.createdAt).toLocaleDateString() }}</div>
        <div class="mobile-card-grid">
          <span>Доступ</span>
          <span>
            <span class="badge" :class="{ active: u.subscriptionStatus === SubscriptionStatusValue.Active }">
              {{ statusLabel(u.subscriptionStatus) }}
            </span>
          </span>
          <span>Раскладов</span>
          <strong>{{ u.totalReadings }}</strong>
          <span>Отзывов</span>
          <strong>{{ u.totalFeedbacks }}</strong>
          <span>Баллов</span>
          <strong>{{ u.totalScore }}</strong>
        </div>
        <div v-if="u.subscriptionExpiresAt" class="expiry mobile-expiry">
          до {{ new Date(u.subscriptionExpiresAt).toLocaleDateString() }}
        </div>
        <span class="mobile-open">Открыть профиль →</span>
        </button>
      </li>
    </ul>
  </template>
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
.user-link {
  text-align: left;
  overflow-wrap: anywhere;
  color: #f5c26b;
  min-height: 44px;
}
.user-link:hover {
  text-decoration: underline;
}
.user-link:focus-visible,
.admin-user-card:focus-visible {
  outline: 2px solid #f5c26b;
  outline-offset: 3px;
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
.table-scroll {
  width: 100%;
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}
.admin-user-cards {
  display: none;
}
@media (max-width: 768px) {
  .table-scroll {
    display: none;
  }
  .admin-user-cards {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    padding: 0;
    margin: 0;
    list-style: none;
  }
  .admin-user-card {
    display: block;
    width: 100%;
    text-align: left;
    padding: 0.85rem;
    border: 1px solid rgba(245, 194, 107, 0.18);
    border-radius: 10px;
    background: rgba(0, 0, 0, 0.2);
    cursor: pointer;
  }
  .mobile-open {
    display: block;
    margin-top: 0.75rem;
    color: #f5c26b;
    font-size: 0.8rem;
  }
  .mobile-card-head {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 0.6rem;
  }
  .mobile-card-head strong {
    overflow-wrap: anywhere;
    color: rgba(224, 212, 186, 0.95);
  }
  .mobile-card-meta {
    margin-top: 0.25rem;
    color: rgba(224, 212, 186, 0.55);
  }
  .mobile-card-grid {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    gap: 0.45rem 0.75rem;
    margin-top: 0.75rem;
    color: rgba(224, 212, 186, 0.78);
    font-size: 0.82rem;
  }
  .mobile-card-grid span:nth-child(odd) {
    color: rgba(224, 212, 186, 0.55);
  }
  .mobile-expiry {
    display: block;
    margin-left: 0;
    margin-top: 0.6rem;
  }
  .mobile-hide {
    display: none;
  }
  .admin-table td,
  .admin-table thead th {
    padding: 0.45rem 0.35rem;
    font-size: 0.8rem;
  }
}
</style>
