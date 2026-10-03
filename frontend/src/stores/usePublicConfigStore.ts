import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { publicApi, type PublicConfig } from '@/api/publicApi'
import { isLocalStaticPreview } from '@/utils/runtime'

export const usePublicConfigStore = defineStore('publicConfig', () => {
  const supportEmail = ref<string>('')
  const paymentsEnabled = ref(false)
  const paymentProduct = ref<PublicConfig['paymentProduct']>(null)
  const paidProduct = computed(() => ({
    title: 'Полный доступ',
    price: paymentProduct.value
      ? new Intl.NumberFormat('ru-RU', { style: 'currency', currency: paymentProduct.value.currency, maximumFractionDigits: 2 }).format(paymentProduct.value.amount)
      : '…',
    period: paymentProduct.value ? `${paymentProduct.value.accessDays} дней` : '…',
  }))
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
      const product = config.paymentProduct
      paymentProduct.value = product && Number.isFinite(product.amount) && product.amount > 0
        && product.currency === 'RUB' && Number.isInteger(product.accessDays) && product.accessDays > 0 ? product : null
    } catch {
      supportEmail.value = ''
      paymentsEnabled.value = false
    } finally {
      loaded.value = true
    }
  }

  return { supportEmail, paymentsEnabled, paymentProduct, paidProduct, loaded, load }
})
