import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import AdminSubscriptionBadge from '@/components/admin/AdminSubscriptionBadge.vue'
import { adminPaidSubscriptionFixture } from '../fixtures/admin'

describe('AdminSubscriptionBadge', () => {
  it.each([7, 30])('shows the actual %i day purchase separately from expired access', days => {
    const wrapper = mount(AdminSubscriptionBadge, { props: { subscription: {
      ...adminPaidSubscriptionFixture, isActive: false, status: 2,
      lastPurchase: { ...adminPaidSubscriptionFixture.lastPurchase, accessDays: days },
    } } })
    expect(wrapper.get('[data-testid="admin-subscription-purchase"]').text()).toContain(`PRO · ${days} дней`)
    expect(wrapper.get('[data-testid="admin-subscription-purchase"]').text()).toContain('499')
    expect(wrapper.get('[data-testid="admin-subscription-status"]').text()).toBe('Доступ истёк')
  })
  it('never calls a manual or unknown active grant a purchase', () => {
    const wrapper = mount(AdminSubscriptionBadge, { props: { subscription: { ...adminPaidSubscriptionFixture, lastPurchase: null } } })
    expect(wrapper.text()).toContain('Доступ активен')
    expect(wrapper.text()).toContain('Без данных о покупке')
    expect(wrapper.find('[data-testid="admin-subscription-purchase"]').exists()).toBe(false)
  })
})
