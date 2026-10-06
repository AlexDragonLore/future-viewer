import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'

const createPayment = vi.fn()
vi.mock('@/api/paymentApi', () => ({ paymentApi: { createAccessPayment: (acceptance: unknown) => createPayment(acceptance) } }))
vi.mock('@/content/legal', () => ({ legalPublication: { isVerified: false }, legalDocumentVersions: { offer: 'offer-current' }, loadLegalDocuments: vi.fn().mockResolvedValue(undefined), isApprovedProcessorUrl: () => false }))
import SubscriptionBanner from '@/components/SubscriptionBanner.vue'

function mountBanner() {
  return mount(SubscriptionBanner, { global: { stubs: { RouterLink: true } } })
}

function enablePayments() {
  const config = usePublicConfigStore()
  config.paymentsEnabled = true
  config.paymentProducts = [
    { tariffCode: 'pro-7d', amount: 99, currency: 'RUB', accessDays: 7 },
    { tariffCode: 'pro-30d', amount: 299, currency: 'RUB', accessDays: 30 },
  ]
  return config
}

describe('SubscriptionBanner', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    createPayment.mockReset().mockResolvedValue({ confirmationUrl: '' })
  })

  it('does not offer checkout unless the server explicitly enables it', () => {
    const wrapper = mountBanner()
    expect(wrapper.find('[data-testid="payments-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('button').exists()).toBe(false)
    expect(createPayment).not.toHaveBeenCalled()
  })

  it('requires a separate offer acceptance and blocks a previously enabled checkout after disablement', async () => {
    const config = enablePayments()
    const wrapper = mountBanner()
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="payment-offer-acceptance"]').setValue(true)
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(createPayment).toHaveBeenCalledTimes(1)
    expect(createPayment).toHaveBeenCalledWith({ offerAccepted: true, offerVersion: 'offer-current', tariffCode: 'pro-7d' })
    config.paymentsEnabled = false
    await flushPromises()
    expect(wrapper.find('button').exists()).toBe(false)
  })

  it('shows both prices and submits the selected monthly tariff', async () => {
    enablePayments()
    const wrapper = mountBanner()
    expect(wrapper.text()).toContain('7 дней')
    expect(wrapper.text()).toContain('99')
    expect(wrapper.text()).toContain('30 дней')
    expect(wrapper.text()).toContain('299')
    await wrapper.get('[data-testid="tariff-pro-30d"]').setValue(true)
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="payment-offer-acceptance"]').setValue(true)
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(createPayment).toHaveBeenCalledWith({ offerAccepted: true, offerVersion: 'offer-current', tariffCode: 'pro-30d' })
  })

  it('does not create payments without available tariff data', () => {
    usePublicConfigStore().paymentsEnabled = true
    const wrapper = mountBanner()
    expect(wrapper.find('[data-testid="payments-unavailable"]').exists()).toBe(true)
    expect(createPayment).not.toHaveBeenCalled()
  })

  it('blocks repeated payment clicks and tariff changes while a payment is being created', async () => {
    enablePayments()
    let resolvePayment!: (value: unknown) => void
    createPayment.mockImplementationOnce(() => new Promise(resolve => { resolvePayment = resolve }))
    const wrapper = mountBanner()
    await wrapper.get('[data-testid="payment-offer-acceptance"]').setValue(true)
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    expect(wrapper.get('fieldset').attributes('disabled')).toBeDefined()
    await wrapper.get('button').trigger('click')
    expect(createPayment).toHaveBeenCalledTimes(1)
    resolvePayment({ confirmationUrl: '' })
    await flushPromises()
    expect(wrapper.get('fieldset').attributes('disabled')).toBeUndefined()
  })
})
