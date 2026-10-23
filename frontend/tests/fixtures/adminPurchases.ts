import type { AdminPurchase } from '@/types/admin'
import { adminUserDetailFixture } from './admin'

export const adminPurchaseFixtures: AdminPurchase[] = Array.from({ length: 26 }, (_, index) => ({
  publicId: 'dddddddd-dddd-4ddd-8ddd-' + String(index + 1).padStart(12, '0'),
  userId: index === 1 ? null : adminUserDetailFixture.id,
  userEmail: index === 1 ? null : adminUserDetailFixture.email,
  tariffCode: index % 2 === 0 ? 'pro-30d' : 'pro-7d',
  accessDays: index % 2 === 0 ? 30 : 7,
  amount: index % 2 === 0 ? 299 : 99,
  currency: 'RUB',
  status: index < 21 ? 'paid' : index === 21 ? 'refunded' : index === 22 ? 'canceled' : index === 23 ? 'failed' : 'pending',
  provider: 'yoomoney',
  providerPaymentId: index === 25 ? null : 'fixture-provider-payment-' + (index + 1),
  createdAt: new Date(Date.UTC(2026, 9, 20, 12) - index * 86400_000).toISOString(),
  completedAt: index < 22 ? new Date(Date.UTC(2026, 9, 20, 12, 5) - index * 86400_000).toISOString() : null,
}))
