<script setup lang="ts">
import { computed } from 'vue'
import { SubscriptionStatusValue } from '@/types'
import type { AdminSubscription } from '@/types/admin'

const props = defineProps<{ subscription?: AdminSubscription | null; showPurchaseDate?: boolean }>()
const purchase = computed(() => props.subscription?.lastPurchase)
const status = computed(() => {
  const value = props.subscription
  if (!value) return 'Без подписки'
  if (value.isActive) return 'Доступ активен'
  if (value.status === SubscriptionStatusValue.Cancelled) return 'Доступ отменён'
  if (value.status === SubscriptionStatusValue.Expired || value.status === SubscriptionStatusValue.Active) return 'Доступ истёк'
  return 'Без подписки'
})
const tariff = computed(() => {
  if (!purchase.value) return ''
  const days = purchase.value.accessDays
  return days === 7 || days === 30 ? `PRO · ${days} дней` : `${purchase.value.tariffCode} · ${days} дн.`
})
const price = computed(() => {
  if (!purchase.value) return ''
  try {
    return new Intl.NumberFormat('ru-RU', { style: 'currency', currency: purchase.value.currency, maximumFractionDigits: 2 }).format(purchase.value.amount)
  } catch { return `${purchase.value.amount} ${purchase.value.currency}` }
})
</script>

<template>
  <span class="subscription" data-testid="admin-subscription">
    <span v-if="purchase" class="purchase" data-testid="admin-subscription-purchase" :title="`Последняя покупка: ${new Date(purchase.completedAt).toLocaleString('ru-RU')}`">
      Куплено: {{ tariff }} · {{ price }}<template v-if="showPurchaseDate"> · <time :datetime="purchase.completedAt">{{ new Date(purchase.completedAt).toLocaleDateString('ru-RU') }}</time></template>
    </span>
    <span class="status" :class="{ active: subscription?.isActive }" data-testid="admin-subscription-status">
      {{ status }}<template v-if="subscription?.isActive && subscription.expiresAt"> до <time :datetime="subscription.expiresAt">{{ new Date(subscription.expiresAt).toLocaleDateString('ru-RU') }}</time></template>
    </span>
    <span v-if="subscription?.isActive && !purchase" class="status">Без данных о покупке</span>
  </span>
</template>

<style scoped>
.subscription { display: inline-flex; flex-wrap: wrap; align-items: center; gap: 0.15rem 0.5rem; min-width: 0; font-size: 0.7rem; line-height: 1.4; overflow-wrap: anywhere; }
.purchase { color: #f5c26b; }
.status { color: rgba(224, 212, 186, 0.55); }
.status.active { color: #98e09d; }
</style>
