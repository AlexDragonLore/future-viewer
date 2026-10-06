<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { ArrowRight, Check } from 'lucide-vue-next'
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
}>()

const emit = defineEmits<{
  (e: 'error', message: string): void
}>()

const loading = ref(false)
const error = ref<string | null>(null)
const offerAccepted = ref(false)
const selectedTariff = ref('')
const tariffGroupName = useId()
const publicConfig = usePublicConfigStore()
const auth = useAuthStore()
const paymentsAvailable = computed(() => publicConfig.paymentsEnabled && publicConfig.paidProducts.length > 0)
const selectedProduct = computed(() => publicConfig.paidProducts.find(product => product.tariffCode === selectedTariff.value))

watch(() => publicConfig.paidProducts, products => {
  if (!products.some(product => product.tariffCode === selectedTariff.value)) {
    selectedTariff.value = products[0]?.tariffCode ?? ''
  }
}, { immediate: true })

async function createPayment() {
  if (loading.value || !offerAccepted.value || !paymentsAvailable.value || !selectedProduct.value) return
  const tariffCode = selectedProduct.value.tariffCode
  loading.value = true
  error.value = null
  try {
    await loadLegalDocuments()
    const result = await paymentApi.createAccessPayment({
      offerAccepted: offerAccepted.value,
      offerVersion: legalDocumentVersions.offer,
      tariffCode,
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
      <div class="title">{{ props.message ?? 'Полный доступ' }}</div>
      <div class="description">Безлимитные расклады · без автосписаний</div>
    </div>
    <fieldset class="tariff-options" :disabled="loading">
      <legend>Выбери срок доступа</legend>
      <label
        v-for="product in publicConfig.paidProducts"
        :key="product.tariffCode"
        class="tariff-option"
        :class="{ selected: selectedTariff === product.tariffCode }"
      >
        <input
          class="tariff-input"
          v-model="selectedTariff"
          type="radio"
          :name="tariffGroupName"
          :value="product.tariffCode"
          :data-testid="`tariff-${product.tariffCode}`"
        />
        <span class="tariff-heading">
          <span class="tariff-name">{{ product.accessDays === 7 ? 'Неделя' : product.accessDays === 30 ? 'Месяц' : 'Доступ' }}</span>
          <span class="selection-mark" aria-hidden="true">
            <Check v-if="selectedTariff === product.tariffCode" :size="12" :stroke-width="2.5" />
          </span>
        </span>
        <span class="tariff-details">
          <span class="tariff-price">{{ product.price }}</span>
          <span class="tariff-period">{{ product.period }}</span>
        </span>
      </label>
    </fieldset>
    <label class="offer-acceptance">
      <input v-model="offerAccepted" type="checkbox" data-testid="payment-offer-acceptance" />
      <span>Принимаю условия <RouterLink to="/legal/offer">оферты</RouterLink></span>
    </label>
    <button class="checkout-button" :disabled="loading || !offerAccepted || !selectedProduct" @click="createPayment">
      <span>{{ loading ? 'Создаём платёж…' : props.buttonLabel ?? 'Оплатить доступ' }}</span>
      <ArrowRight v-if="!loading" :size="17" aria-hidden="true" />
    </button>
    <div v-if="error" class="error">{{ error }}</div>
  </div>
  <p v-else class="subscription-banner" data-testid="payments-unavailable">Оплата временно недоступна.</p>
</template>

<style scoped>
.subscription-banner {
  display: grid;
  gap: 1rem;
  padding: 1.25rem;
  border-radius: 14px;
  background: rgba(10, 6, 22, 0.38);
}
.info {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}
.title {
  color: #f0e5f6;
  font-size: 0.95rem;
  font-weight: 600;
  line-height: 1.5;
}
.description {
  font-size: 0.75rem;
  line-height: 1.6;
  color: #a59bb6;
}
.tariff-options {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  min-width: 0;
  gap: 0.75rem;
  margin: 0;
  padding: 0;
  border: 0;
}
.tariff-options legend {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
.tariff-option {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  min-width: 0;
  padding: 1rem;
  border-radius: 10px;
  border: 1px solid rgba(186, 164, 216, 0.18);
  background: rgba(255, 255, 255, 0.025);
  cursor: pointer;
  transition: border-color 0.2s, background-color 0.2s;
}
.tariff-option:hover {
  border-color: rgba(245, 194, 107, 0.5);
}
.tariff-option.selected {
  border-color: rgba(245, 194, 107, 0.7);
  background: linear-gradient(135deg, rgba(245, 194, 107, 0.1), rgba(245, 194, 107, 0.035));
}
.tariff-option:has(.tariff-input:focus-visible) {
  outline: 2px solid #f5c26b;
  outline-offset: 2px;
}
.tariff-input {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  margin: 0;
  opacity: 0;
  cursor: pointer;
}
.tariff-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.25rem;
}
.tariff-name {
  font-size: 0.8rem;
  font-weight: 500;
  color: #d6cadf;
}
.selection-mark {
  display: grid;
  place-items: center;
  width: 17px;
  height: 17px;
  flex-shrink: 0;
  border: 1px solid rgba(186, 164, 216, 0.3);
  border-radius: 50%;
}
.selected .selection-mark {
  color: #25192b;
  background: #f5c26b;
  border-color: #f5c26b;
}
.tariff-details {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}
.tariff-period {
  font-size: 0.72rem;
  color: #a59bb6;
  white-space: nowrap;
}
.tariff-price {
  font-size: 1.65rem;
  font-weight: 600;
  line-height: 1.15;
  letter-spacing: -0.04em;
  color: #f4e8d9;
  white-space: nowrap;
}
.selected .tariff-price {
  color: #f8d695;
}
.checkout-button {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.6rem;
  min-height: 44px;
  padding: 0.7rem 1rem;
  border: 1px solid transparent;
  border-radius: 9px;
  background: #f5c26b;
  color: #25192b;
  font: inherit;
  font-size: 0.85rem;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 0.2s, opacity 0.2s;
}
.checkout-button:hover:not(:disabled) {
  background: #f8d695;
}
.checkout-button:focus-visible {
  outline: 2px solid #f5c26b;
  outline-offset: 3px;
}
.checkout-button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
.error {
  font-size: 0.75rem;
  color: #fca5a5;
}
.offer-acceptance {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  color: #b3a7c3;
  font-size: 0.72rem;
  line-height: 1.5;
}
.offer-acceptance input {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
  margin-top: 0.12rem;
  accent-color: #f5c26b;
}
.offer-acceptance a {
  color: #f5c26b;
  text-decoration: underline;
}
@media (max-width: 640px) {
  .subscription-banner {
    padding: 1rem;
  }
  .tariff-options {
    gap: 0.5rem;
  }
  .tariff-option {
    padding: 0.75rem 0.65rem;
  }
  .tariff-price {
    font-size: 1.5rem;
  }
}
</style>
