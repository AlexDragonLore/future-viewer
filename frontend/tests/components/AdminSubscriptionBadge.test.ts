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
  it('keeps compact purchase details accessible and emits the user-drawer action', async () => {
    const wrapper = mount(AdminSubscriptionBadge, { props: { subscription: adminPaidSubscriptionFixture, compact: true } })
    expect(wrapper.text()).toBe('30д')
    const button = wrapper.get('button')
    expect(button.attributes('aria-label')).toContain('499')
    expect(button.attributes('aria-label')).toContain('Доступ активен')
    expect(button.attributes('title')).toContain('01.10.2026')
    await button.trigger('click')
    expect(wrapper.emitted('open')).toHaveLength(1)
  })
  it('renders no free-access label and uses an access icon for manual grants', () => {
    const empty = mount(AdminSubscriptionBadge, { props: { subscription: null, compact: true } })
    expect(empty.text()).toBe('')
    expect(empty.find('button').exists()).toBe(false)
    const manual = mount(AdminSubscriptionBadge, { props: { subscription: { ...adminPaidSubscriptionFixture, lastPurchase: null }, compact: true } })
    expect(manual.text()).toBe('')
    expect(manual.get('button').attributes('aria-label')).toContain('Без данных о покупке')
    expect(manual.find('[data-testid="admin-subscription-purchase"]').exists()).toBe(false)
  })

})
