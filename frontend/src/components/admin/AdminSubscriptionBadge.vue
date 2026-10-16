<script setup lang="ts">
import { computed } from 'vue'
import { Crown, ShieldCheck } from 'lucide-vue-next'
import { SubscriptionStatusValue } from '@/types'
import type { AdminSubscription } from '@/types/admin'

const props = defineProps<{ subscription?: AdminSubscription | null; showPurchaseDate?: boolean; compact?: boolean }>()
const emit = defineEmits<{ open: [] }>()
const purchase = computed(() => props.subscription?.lastPurchase)
const status = computed(() => {
  const value = props.subscription
  if (!value) return ''
  if (value.isActive) return 'Доступ активен'
  if (value.status === SubscriptionStatusValue.Cancelled) return 'Доступ отменён'
  if (value.status === SubscriptionStatusValue.Expired || value.status === SubscriptionStatusValue.Active) return 'Доступ истёк'
  return ''
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
const compactLabel = computed(() => {
  const detail = purchase.value
    ? `Последняя покупка: ${tariff.value}, ${price.value}, ${new Date(purchase.value.completedAt).toLocaleDateString('ru-RU')}. `
    : 'Без данных о покупке. '
  const expiry = props.subscription?.isActive && props.subscription.expiresAt
    ? ` до ${new Date(props.subscription.expiresAt).toLocaleDateString('ru-RU')}` : ''
  return `${detail}${status.value}${expiry}. Открыть карточку пользователя`
})
</script>

<template>
  <button v-if="compact && (purchase || subscription?.isActive)" type="button" class="compact-subscription" :class="{ inactive: !subscription?.isActive }" :title="compactLabel" :aria-label="compactLabel" data-testid="admin-subscription-button" @click="emit('open')">
    <Crown v-if="purchase" :size="14" aria-hidden="true" /><ShieldCheck v-else :size="14" aria-hidden="true" />
    <span v-if="purchase" data-testid="admin-subscription-purchase">{{ purchase.accessDays }}д</span>
  </button>
  <span v-else-if="!compact && (purchase || status)" class="subscription" data-testid="admin-subscription">
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
.compact-subscription { display: inline-flex; align-items: center; justify-content: center; gap: 0.2rem; height: 28px; min-width: 32px; flex-shrink: 0; padding: 0 0.2rem; color: #f5c26b; font-size: 0.68rem; white-space: nowrap; border-radius: 4px; }
.compact-subscription.inactive { color: #b7a688; }
.compact-subscription:hover { background: rgba(245, 194, 107, 0.1); }
.compact-subscription:focus-visible { outline: 2px solid #f5c26b; outline-offset: -2px; }
@media (max-width: 640px) { .compact-subscription { height: 44px; min-width: 44px; } }
.subscription { display: inline-flex; flex-wrap: wrap; align-items: center; gap: 0.15rem 0.5rem; min-width: 0; font-size: 0.7rem; line-height: 1.4; overflow-wrap: anywhere; }
.purchase { color: #f5c26b; }
.status { color: rgba(224, 212, 186, 0.55); }
.status.active { color: #98e09d; }
</style>
