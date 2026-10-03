<script setup lang="ts">
import { computed, ref } from 'vue'
import { paymentApi } from '@/api/paymentApi'
import { extractApiError } from '@/api/httpClient'
import { isApprovedProcessorUrl, legalDocumentVersions, loadLegalDocuments } from '@/content/legal'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'
import { useAuthStore } from '@/stores/useAuthStore'
import { trackGoalOnce } from '@/analytics/metrika'
import { savePendingPayment } from '@/utils/pendingPayment'

const props = defineProps<{
  buttonLabel?: string
  message?: string
  priceLabel: string
}>()

const emit = defineEmits<{
  (e: 'error', message: string): void
}>()

const loading = ref(false)
const error = ref<string | null>(null)
const offerAccepted = ref(false)
const publicConfig = usePublicConfigStore()
const auth = useAuthStore()
const paymentsAvailable = computed(() => publicConfig.paymentsEnabled)

async function createPayment() {
  if (loading.value || !offerAccepted.value || !paymentsAvailable.value) return
  loading.value = true
  error.value = null
  try {
    await loadLegalDocuments()
    const result = await paymentApi.createAccessPayment({
      offerAccepted: offerAccepted.value,
      offerVersion: legalDocumentVersions.offer,
    })
    if (result.confirmationUrl) {
      if (!isApprovedProcessorUrl(result.confirmationUrl, 'payment')) {
        error.value = 'Переход к непроверенному платёжному адресу заблокирован.'
        emit('error', error.value)
        return
      }
      if (auth.userId) savePendingPayment(result.paymentId, auth.userId)
      trackGoalOnce('payment_started', result.paymentId)
      window.location.assign(result.confirmationUrl)
    } else {
      error.value = 'Платёж не удалось инициализировать'
      emit('error', error.value)
    }
  } catch (e) {
    error.value = extractApiError(e, 'Не удалось создать платёж')
    emit('error', error.value)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div v-if="paymentsAvailable" class="subscription-banner">
    <div class="info">
      <div class="title">{{ props.message ?? 'Оплати доступ' }}</div>
      <div class="price">{{ props.priceLabel }} · безлимитные расклады · без автосписаний</div>
    </div>
    <label class="offer-acceptance">
      <input v-model="offerAccepted" type="checkbox" data-testid="payment-offer-acceptance" />
      <span>Принимаю <RouterLink to="/legal/offer">оферту</RouterLink> перед оплатой.</span>
    </label>
    <button class="glow-button" :disabled="loading || !offerAccepted" @click="createPayment">
      {{ loading ? '…' : props.buttonLabel ?? 'Оплатить доступ' }}
    </button>
    <div v-if="error" class="error">{{ error }}</div>
  </div>
  <p v-else class="subscription-banner" data-testid="payments-unavailable">Оплата временно недоступна.</p>
</template>

<style scoped>
.subscription-banner {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  padding: 0.75rem 1rem;
  border-radius: 10px;
  border: 1px solid rgba(245, 194, 107, 0.4);
  background: linear-gradient(135deg, rgba(245, 194, 107, 0.08), rgba(245, 194, 107, 0.02));
}
.info {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  flex: 1;
  min-width: min(14rem, 100%);
}
.title {
  font-family: 'Cinzel', serif;
  color: #f5c26b;
  font-size: 0.9rem;
  letter-spacing: 0.08em;
}
.price {
  font-size: 0.75rem;
  color: rgba(224, 212, 186, 0.75);
}
.error {
  flex-basis: 100%;
  font-size: 0.75rem;
  color: #fca5a5;
}
.offer-acceptance {
  display: flex;
  flex-basis: 100%;
  align-items: flex-start;
  gap: 0.5rem;
  color: rgba(224, 212, 186, 0.72);
  font-size: 0.72rem;
}
.offer-acceptance input {
  margin-top: 0.16rem;
  accent-color: #f5c26b;
}
.offer-acceptance a {
  color: #f5c26b;
  text-decoration: underline;
}
@media (max-width: 640px) {
  .subscription-banner {
    align-items: stretch;
  }
  .subscription-banner .glow-button {
    width: 100%;
  }
}
</style>
