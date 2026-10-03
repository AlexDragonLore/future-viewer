import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'

const createPayment = vi.fn()
vi.mock('@/api/paymentApi', () => ({ paymentApi: { createAccessPayment: (acceptance: unknown) => createPayment(acceptance) } }))
vi.mock('@/content/legal', () => ({ legalPublication: { isVerified: false }, legalDocumentVersions: { offer: 'offer-current' }, loadLegalDocuments: vi.fn().mockResolvedValue(undefined), isApprovedProcessorUrl: () => false }))
import SubscriptionBanner from '@/components/SubscriptionBanner.vue'

function mountBanner() {
  return mount(SubscriptionBanner, { props: { priceLabel: '100 ₽' }, global: { stubs: { RouterLink: true } } })
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
    const config = usePublicConfigStore()
    config.paymentsEnabled = true
    const wrapper = mountBanner()
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="payment-offer-acceptance"]').setValue(true)
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(createPayment).toHaveBeenCalledTimes(1)
    expect(createPayment).toHaveBeenCalledWith({ offerAccepted: true, offerVersion: 'offer-current' })
    config.paymentsEnabled = false
    await flushPromises()
    expect(wrapper.find('button').exists()).toBe(false)
  })
})
