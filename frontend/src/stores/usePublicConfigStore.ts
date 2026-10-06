import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { publicApi, type PaymentProduct } from '@/api/publicApi'
import { isLocalStaticPreview } from '@/utils/runtime'

export const usePublicConfigStore = defineStore('publicConfig', () => {
  const supportEmail = ref<string>('')
  const paymentsEnabled = ref(false)
  const paymentProducts = ref<PaymentProduct[]>([])
  const paidProducts = computed(() => paymentProducts.value.map(product => ({
    ...product,
    price: new Intl.NumberFormat('ru-RU', { style: 'currency', currency: product.currency, minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(product.amount),
    period: `${product.accessDays} дней`,
  })))
  const loaded = ref(false)

  async function load() {
    if (loaded.value) return
    if (isLocalStaticPreview()) {
      loaded.value = true
      return
    }
    try {
      const config = await publicApi.getConfig()
      supportEmail.value = config.supportEmail ?? ''
      paymentsEnabled.value = config.paymentsEnabled === true
      const legacyProduct = config.paymentProduct
      const products = config.paymentProducts ?? (legacyProduct ? [{
        ...legacyProduct,
        tariffCode: legacyProduct.tariffCode || 'pro-30d',
      }] : [])
      paymentProducts.value = products.filter(product => product
        && typeof product.tariffCode === 'string' && product.tariffCode.trim().length > 0
        && Number.isFinite(product.amount) && product.amount > 0
        && product.currency === 'RUB' && Number.isInteger(product.accessDays) && product.accessDays > 0)
        .sort((a, b) => a.accessDays - b.accessDays)
      paymentsEnabled.value = paymentsEnabled.value && paymentProducts.value.length > 0
    } catch {
      supportEmail.value = ''
      paymentsEnabled.value = false
      paymentProducts.value = []
    } finally {
      loaded.value = true
    }
  }

  return { supportEmail, paymentsEnabled, paymentProducts, paidProducts, loaded, load }
})
