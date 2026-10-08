<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { paymentApi } from '@/api/paymentApi'
import { getPendingPayment, clearPendingPayment } from '@/utils/pendingPayment'
import { trackGoalOnce } from '@/analytics/metrika'
import { getGuestContinuation } from '@/utils/guestReading'

const router = useRouter()
const auth = useAuthStore()
const refreshing = ref(true)
const orderPaid = ref<boolean | null>(null)

onMounted(async () => {
  if (auth.isAuthenticated) {
    const paymentId = getPendingPayment(auth.userId)
    if (paymentId) {
      orderPaid.value = false
      try {
        const payment = await paymentApi.status(paymentId)
        if (payment.paid) {
          orderPaid.value = true
          trackGoalOnce('payment_completed', paymentId)
          clearPendingPayment()
        } else if (['canceled', 'failed'].includes(payment.status)) clearPendingPayment()
      } catch { /* A failed check cannot imply a paid order. The user can retry. */ }
    }
    await auth.refreshSubscription()
  }
  refreshing.value = false
})

const activated = computed(() => orderPaid.value ?? auth.isSubscribed)
const continueGuestReading = computed(() => activated.value && Boolean(getGuestContinuation()))

function goHome() {
  router.replace(continueGuestReading.value ? '/result' : '/')
}
</script>

<template>
  <main class="payment-page min-h-screen flex items-center justify-center px-4 sm:px-6 py-10">
    <section class="mystic-card p-6 sm:p-8 w-full max-w-md text-center">
      <div class="payment-kicker text-mystic-accent text-xs tracking-[0.4em] mb-3">✦ ПЛАТЁЖ ✦</div>

      <template v-if="refreshing">
        <h1 class="font-display text-3xl gold-text mb-4">Проверяем оплату…</h1>
        <p class="text-mystic-silver/70">Секунду, обновляем статус.</p>
      </template>

      <template v-else-if="activated">
        <h1 class="font-display text-3xl gold-text mb-4">Доступ активен</h1>
        <p class="text-mystic-silver/70 mb-6">
          Спасибо! Теперь тебе доступны все расклады без ограничений.
        </p>
        <button class="glow-button w-full" @click="goHome">{{ continueGuestReading ? 'Продолжить мой расклад' : 'К раскладам' }}</button>
      </template>

      <template v-else>
        <h1 class="font-display text-3xl gold-text mb-4">Платёж в обработке</h1>
        <p class="text-mystic-silver/70 mb-6">
          ЮKassa ещё не подтвердила оплату. Доступ активируется после подтверждения —
          обычно это занимает несколько секунд. Обнови страницу или зайди позже.
        </p>
        <button class="glow-button w-full" @click="goHome">На главную</button>
      </template>
    </section>
  </main>
</template>

<style scoped>
@media (max-width: 640px) {
  .payment-page {
    align-items: flex-start;
    padding-top: 2.5rem;
  }
  .payment-kicker {
    letter-spacing: 0.14em;
  }
  h1 {
    font-size: 1.9rem;
    line-height: 1.15;
  }
}
</style>
