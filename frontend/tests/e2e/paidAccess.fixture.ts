import type { BrowserContext } from '@playwright/test'
import { fullReading, guestResponse, previewReading, verifiedAuth } from './guestReading.fixture'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

interface PaidAccessFixtureOptions {
  authenticated?: boolean
  subscribed?: boolean
  paymentsEnabled?: boolean
  amount?: number
  accessDays?: number
}

/**
 * Install before visiting the local Vite dev server, then open /result (default)
 * or / with authenticated:false. All APIs are mocked and all external requests
 * are blocked, including production analytics and payment providers.
 */
export async function installPaidAccessFixture(context: BrowserContext, options: PaidAccessFixtureOptions = {}) {
  const authenticated = options.authenticated ?? true
  const subscribed = options.subscribed ?? false
  const checkoutAttempts: unknown[] = []
  const unexpectedApis: string[] = []
  await context.addInitScript(({ authenticated, session }) => {
    localStorage.clear()
    if (!authenticated) return
    localStorage.setItem('fv_token', session.accessToken)
    localStorage.setItem('fv_email', session.email)
    localStorage.setItem('fv_user_id', session.userId)
    localStorage.setItem('fv_guest_reading_v1', JSON.stringify({
      ticket: 'qa-encrypted-ticket',
      expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
    }))
  }, { authenticated, session: verifiedAuth })

  await context.route('**/*', async (route) => {
    const url = new URL(route.request().url())
    if (!['127.0.0.1', 'localhost'].includes(url.hostname)) return route.abort()
    if (!url.pathname.startsWith('/api/')) return route.continue()
    let body: unknown
    switch (url.pathname) {
      case '/api/public/config':
        body = {
          supportEmail: '',
          paymentsEnabled: options.paymentsEnabled ?? true,
          paymentProduct: { amount: options.amount ?? 300, currency: 'RUB', accessDays: options.accessDays ?? 30 },
        }
        break
      case '/api/public/legal-documents': body = legalDocumentsResponse; break
      case '/api/subscription/status':
        body = { status: subscribed ? 1 : 0, expiresAt: null, isActive: subscribed,
          canCreateFreeReading: subscribed, freeReadingsUsedToday: 1, freeReadingsDailyLimit: 1 }
        break
      case '/api/announcements/unread': body = []; break
      case '/api/privacy/settings': body = { historyEnabled: false }; break
      case '/api/readings/guest': body = guestResponse(); break
      case '/api/readings/guest/preview': body = previewReading; break
      case '/api/readings/guest/unlock': body = fullReading; break
      case '/api/payments/subscribe':
        checkoutAttempts.push(route.request().postDataJSON())
        await route.fulfill({ status: 503, json: { message: 'Локальная проверка: реальный платёж не создаётся.' } })
        return
      default:
        unexpectedApis.push(url.pathname)
        await route.fulfill({ status: 404, json: { message: 'Unexpected mock API request' } })
        return
    }
    await route.fulfill({ json: body })
  })
  return { checkoutAttempts, unexpectedApis }
}
