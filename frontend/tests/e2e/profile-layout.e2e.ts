import { expect, test, type Page } from '@playwright/test'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

const longEmail = `${'mobile.profile.'.repeat(4)}reader@${'long-domain-'.repeat(3)}example.com`
const memoryText = `Предпочитает спокойные и подробные объяснения: ${'длиннаязаметка'.repeat(12)}`
const feedbackQuestion = `На что обратить внимание в ближайшее время? ${'длинныйвопрос'.repeat(16)}`
const scenarios = [
  { name: 'free', email: 'mobile@example.com', active: false },
  { name: 'free with long email', email: longEmail, active: false },
  { name: 'active with long email', email: longEmail, active: true },
]

async function openProfile(page: Page, email: string, active: boolean) {
  const unexpectedRequests: string[] = []
  const responses: Record<string, unknown> = {
    '/api/public/config': {
      supportEmail: '', paymentsEnabled: true,
      paymentProduct: { tariffCode: 'pro-30d', amount: 299, currency: 'RUB', accessDays: 30 },
      paymentProducts: [
        { tariffCode: 'pro-7d', amount: 99, currency: 'RUB', accessDays: 7 },
        { tariffCode: 'pro-30d', amount: 299, currency: 'RUB', accessDays: 30 },
      ],
    },
    '/api/public/legal-documents': legalDocumentsResponse,
    '/api/subscription/status': {
      status: active ? 1 : 0, isActive: active, expiresAt: active ? '2030-01-01T00:00:00Z' : null,
      freeReadingsUsedToday: 0, freeReadingsDailyLimit: 1, canCreateIntroReading: false, canCreateFreeReading: true,
    },
    '/api/announcements/unread': [],
    '/api/leaderboard/me': {
      totalScore: 30, feedbackScore: 20, achievementScore: 10, monthlyScore: 30,
      rank: 123, monthlyRank: 45, feedbackCount: 2, averageScore: 10,
    },
    '/api/feedbacks/my': [{
      id: 'layout-feedback', readingId: 'layout-reading', question: feedbackQuestion,
      status: 3, aiScore: 10, createdAt: '2026-10-01T10:00:00Z',
    }],
    '/api/profile/personalization': {
      firstName: 'Александр', lastName: 'Пользователь', birthYear: 1990, isComplete: true,
      memoryRules: [{ id: 'layout-memory', text: memoryText }],
    },
    '/api/privacy/settings': {
      historyEnabled: false, ageConfirmed18: true, personalizationEnabled: true,
      marketingEnabled: false, analyticsEnabled: false,
    },
    '/api/privacy/consents': [{
      id: 'layout-consent', consentType: 'personalization', documentVersion: '2026-10-01',
      acceptedAt: '2026-10-01T10:00:00Z', revokedAt: null, collectionSource: 'profile',
    }],
    '/api/privacy/account-deletion/status': { requested: false },
  }
  await page.route('**/api/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (!path.startsWith('/api/')) return route.fallback()
    if (request.method() !== 'GET' || !(path in responses)) {
      unexpectedRequests.push(`${request.method()} ${path}`)
      return route.fulfill({ status: 400, json: { error: 'Unexpected test request' } })
    }
    await route.fulfill({ json: responses[path] })
  })
  await page.addInitScript((fixtureEmail) => {
    localStorage.setItem('fv_token', 'local-profile-layout-fixture')
    localStorage.setItem('fv_email', fixtureEmail)
    localStorage.setItem('fv_user_id', 'local-profile-layout-user')
  }, email)
  await page.goto('/profile')
  await page.getByTestId('accept-necessary').click()
  await expect(page.getByTestId('profile-feedbacks')).toContainText(feedbackQuestion)
  await expect(page.getByTestId('save-personalization')).toBeEnabled()
  await expect(page.getByTestId('profile-access')).toContainText(
    active ? 'Платный доступ активен' : 'Сейчас включён бесплатный режим',
  )
  await page.evaluate(() => document.fonts.ready)
  return unexpectedRequests
}

for (const width of [320, 393, 640, 768, 1280]) {
  for (const scenario of scenarios) {
    test(`profile fits ${width}px viewport: ${scenario.name}`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 })
      await page.emulateMedia({ reducedMotion: 'reduce' })
      const unexpectedRequests = await openProfile(page, scenario.email, scenario.active)
      await expect(page.getByRole('heading', { level: 1 })).toHaveText(scenario.email)
      await expect(page.locator('.memory-row')).toContainText(memoryText)

      // App clips horizontal overflow, so document.scrollWidth alone can hide the regression.
      const overflowingElements = await page.locator('.profile-page, .profile-page *').evaluateAll(elements =>
        elements.flatMap(element => {
          const rect = element.getBoundingClientRect()
          const style = getComputedStyle(element)
          if (!rect.width || !rect.height || style.visibility === 'hidden') return []
          return rect.left < -1 || rect.right > document.documentElement.clientWidth + 1
            ? [{ element: element.getAttribute('data-testid') ?? element.className, left: rect.left, right: rect.right }]
            : []
        }),
      )
      expect(overflowingElements).toEqual([])
      for (const selector of ['.profile-title', '.memory-row > span']) {
        const clipped = await page.locator(selector).evaluate(element => element.scrollWidth > element.clientWidth + 1)
        expect(clipped, `${selector} must wrap its full text`).toBe(false)
      }

      const acceptance = page.getByTestId('payment-offer-acceptance')
      await expect(page.getByTestId('tariff-pro-7d')).toBeChecked()
      await expect(page.locator('.tariff-option').filter({ hasText: '7 дней' })).toContainText(/99\s*₽/)
      await expect(page.locator('.tariff-option').filter({ hasText: '30 дней' })).toContainText(/299\s*₽/)
      const paymentButton = page.getByRole('button', { name: scenario.active ? 'Продлить доступ' : 'Оплатить доступ', exact: true })
      await expect(paymentButton).toBeDisabled()
      await acceptance.check()
      await expect(acceptance).toBeChecked()
      await paymentButton.scrollIntoViewIfNeeded()
      await expect(paymentButton).toBeInViewport({ ratio: 1 })
      await expect(paymentButton).toBeEnabled()
      await paymentButton.click({ trial: true })
      expect(unexpectedRequests).toEqual([])
    })
  }
}
