<script setup lang="ts">
import { ref } from 'vue'
import { paymentApi } from '@/api/paymentApi'
import { extractApiError } from '@/api/httpClient'

const props = defineProps<{
  message?: string
  priceLabel?: string
}>()

const emit = defineEmits<{
  (e: 'error', message: string): void
}>()

const loading = ref(false)
const error = ref<string | null>(null)

async function subscribe() {
  if (loading.value) return
  loading.value = true
  error.value = null
  try {
    const result = await paymentApi.subscribe()
    if (result.confirmationUrl) {
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
  <div class="subscription-banner">
    <div class="info">
      <div class="title">{{ props.message ?? 'Оформи подписку' }}</div>
      <div class="price">{{ props.priceLabel ?? '300 ₽ / месяц' }} · безлимитные расклады</div>
    </div>
    <button class="glow-button" :disabled="loading" @click="subscribe">
      {{ loading ? '…' : 'Оформить подписку' }}
    </button>
    <div v-if="error" class="error">{{ error }}</div>
  </div>
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
  min-width: 14rem;
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
</style>
