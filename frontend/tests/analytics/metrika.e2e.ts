import { expect, test, type BrowserContext } from '@playwright/test'
import { fullReading, guestResponse, previewReading, verifiedAuth } from '../e2e/guestReading.fixture'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

async function analyticsFixture(context: BrowserContext) {
  const calls: unknown[][] = []
  let scriptRequests = 0
  await context.exposeBinding('__captureMetrika', (_source, args: unknown[]) => { calls.push(args) })
  await context.route('https://mc.yandex.ru/**', async route => {
    if (new URL(route.request().url()).pathname !== '/metrika/tag.js') {
      return route.abort()
    }
    scriptRequests++
    await route.fulfill({ contentType: 'application/javascript', body: `
      window.ym = function () {
        var args = Array.prototype.slice.call(arguments);
        window.__captureMetrika(args);
        if (args[1] === 'init') document.dispatchEvent(new Event('yacounter' + args[0] + 'inited'));
      };
    ` })
  })
  await context.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname
    if (!path.startsWith('/api/')) return route.fallback()
    let body: unknown
    switch (path) {
      case '/api/public/config': body = { supportEmail: '', paymentsEnabled: false }; break
      case '/api/public/legal-documents': body = legalDocumentsResponse; break
      case '/api/readings/guest': body = guestResponse(); break
      case '/api/readings/guest/preview': body = previewReading; break
      case '/api/readings/guest/unlock': body = fullReading; break
      case '/api/auth/verify-email':
      case '/api/auth/login': body = verifiedAuth; break
      case '/api/auth/register':
        expect(route.request().postDataJSON()).toMatchObject({
          personalDataConsentAccepted: true,
          documentVersions: { offer: 'published-offer', personalDataConsent: 'published-personal-data-consent' },
          optionalConsents: { personalization: false, marketing: false, analytics: false },
        })
        body = { userId: verifiedAuth.userId, email: verifiedAuth.email, verificationRequired: true }
        break
      case '/api/subscription/status': body = { isActive: false, canCreateFreeReading: false, freeReadingsUsedToday: 1, freeReadingsDailyLimit: 1 }; break
      case '/api/announcements/unread': body = []; break
      default: return route.fulfill({ status: 404, json: { message: 'Unexpected test request' } })
    }
    await route.fulfill({ status: path === '/api/auth/register' ? 202 : 200, json: body })
  })
  return {
    calls,
    goals: () => calls.filter((call) => call[1] === 'reachGoal').map((call) => call[2]),
    scriptRequests: () => scriptRequests,
  }
}

test('consented guest funnel counts successful verification and unlock once across tabs and reloads', async ({ page, context }) => {
  test.setTimeout(60000)
  const analytics = await analyticsFixture(context)
  await page.goto('/?utm_source=yandex&utm_medium=cpc&utm_campaign=123&utm_content=456&yclid=123456789012&email=PRIVATE_EMAIL#PRIVATE_HASH')
  await expect(page.getByTestId('cookie-preferences')).toBeVisible()
  expect(analytics.scriptRequests()).toBe(0)
  await page.getByTestId('accept-all').click()
  await expect.poll(() => analytics.calls.some((call) => call[1] === 'hit')).toBe(true)
  expect(analytics.scriptRequests()).toBe(1)
  await page.getByRole('button', { name: 'Открыть карту бесплатно' }).click()
  await expect(page.getByTestId('guest-unlock')).toBeVisible({ timeout: 20000 })
  await expect.poll(() => analytics.goals()).toEqual(['guest_reading_started', 'guest_preview_viewed'])
  await page.reload()
  await expect(page.getByTestId('guest-unlock')).toBeVisible({ timeout: 20000 })
  await page.getByRole('link', { name: 'Зарегистрироваться и дочитать' }).click()
  await expect(page.getByRole('heading', { name: 'Регистрация', exact: true })).toBeVisible()
  await expect.poll(() => analytics.goals()).toContain('registration_started')

  await page.getByRole('textbox', { name: 'Электронная почта' }).fill('qa@example.com')
  await page.getByLabel('Пароль', { exact: true }).fill('PRIVATE_PASSWORD123')
  await page.getByTestId('personal-data-consent-acceptance').check()
  await page.getByRole('button', { name: 'Создать', exact: true }).click()
  await expect(page.getByText(/Мы отправили письмо на/)).toBeVisible()
  await expect.poll(() => analytics.goals()).toContain('registration_submitted')

  const emailTab = await context.newPage()
  await emailTab.goto('/verify-email#token=PRIVATE_VERIFICATION_TOKEN')
  await expect(emailTab).toHaveURL(/\/result$/, { timeout: 15000 })
  await expect(emailTab.getByRole('heading', { name: 'Следующий шаг' })).toBeVisible({ timeout: 15000 })
  await expect.poll(() => analytics.goals().filter((goal) => goal === 'email_verified').length).toBe(1)
  await expect.poll(() => analytics.goals().filter((goal) => goal === 'guest_reading_unlocked').length).toBe(1)
  await emailTab.goto('/verify-email#token=PRIVATE_VERIFICATION_TOKEN')
  await expect(emailTab).toHaveURL(/\/$/, { timeout: 15000 })
  expect(analytics.goals().filter((goal) => goal === 'email_verified')).toHaveLength(1)
  expect(analytics.goals().filter((goal) => goal === 'guest_preview_viewed')).toHaveLength(1)
  expect(analytics.goals().filter((goal) => goal === 'guest_reading_unlocked')).toHaveLength(1)

  const transmissions = JSON.stringify(analytics.calls)
  expect(transmissions).not.toMatch(/PRIVATE_|qa-user|qa@example|qa-encrypted-ticket|11111111-1111|На что мне/)
  for (const call of analytics.calls.filter((item) => item[1] === 'reachGoal')) expect(call).toHaveLength(3)
  expect(analytics.calls.find((call) => call[1] === 'hit')?.[2])
    .toBe('http://127.0.0.1:4184/?utm_source=yandex&utm_medium=cpc&utm_campaign=123&utm_content=456&yclid=123456789012')
})

test('necessary-only choice keeps the complete guest journey free of analytics requests', async ({ page, context }) => {
  test.setTimeout(40000)
  const analytics = await analyticsFixture(context)
  await page.goto('/?utm_source=yandex')
  await page.getByTestId('accept-necessary').click()
  await page.getByRole('button', { name: 'Открыть карту бесплатно' }).click()
  await expect(page.getByTestId('guest-unlock')).toBeVisible({ timeout: 20000 })
  await page.getByRole('link', { name: 'Зарегистрироваться и дочитать' }).click()
  await expect(page.getByRole('heading', { name: 'Регистрация', exact: true })).toBeVisible()
  expect(analytics.scriptRequests()).toBe(0)
  expect(analytics.calls).toHaveLength(0)
  expect(await page.evaluate(() => localStorage.getItem('fv_analytics_goals_v1'))).toBeNull()
})

test('revoking analytics stops initialized counters in every open tab', async ({ page, context }) => {
  const analytics = await analyticsFixture(context)
  await page.goto('/')
  await page.getByTestId('accept-all').click()
  await expect.poll(() => analytics.calls.filter((call) => call[1] === 'init').length).toBe(1)
  const secondTab = await context.newPage()
  await secondTab.goto('/about?token=PRIVATE_TOKEN')
  await expect.poll(() => analytics.calls.filter((call) => call[1] === 'init').length).toBe(2)
  await page.getByTestId('change-cookie-settings').click()
  await page.getByTestId('accept-necessary').click()
  await expect.poll(() => analytics.calls.filter((call) => call[1] === 'destruct').length).toBe(2)
  const totalCalls = analytics.calls.length
  await secondTab.getByRole('link', { name: 'Вопросы и ответы', exact: true }).click()
  await expect(secondTab).toHaveURL(/\/faq$/)
  expect(analytics.calls).toHaveLength(totalCalls)
  expect(await secondTab.evaluate(() => localStorage.getItem('fv_analytics_goals_v1'))).toBeNull()
})

test('only a backend-confirmed paid order counts as a payment conversion', async ({ page, context }) => {
  const analytics = await analyticsFixture(context)
  const paymentId = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
  await context.route(`**/api/payments/${paymentId}/status`, route => route.fulfill({
    json: { status: 'succeeded', paid: true },
  }))
  await context.route('**/api/subscription/status', route => route.fulfill({
    json: { isActive: true, canCreateFreeReading: true, freeReadingsUsedToday: 0, freeReadingsDailyLimit: 1 },
  }))
  await page.goto('/')
  await page.getByTestId('accept-all').click()
  await page.evaluate(({ paymentId, auth }) => {
    localStorage.setItem('fv_token', auth.accessToken)
    localStorage.setItem('fv_email', auth.email)
    localStorage.setItem('fv_user_id', auth.userId)
    localStorage.setItem('fv_pending_payment_v1', JSON.stringify({
      paymentId, userId: auth.userId, expires: Date.now() + 3600000,
    }))
  }, { paymentId, auth: verifiedAuth })
  await page.goto('/payment/success?order=PRIVATE_ORDER')
  await expect(page.getByRole('heading', { name: 'Доступ активен' })).toBeVisible()
  await expect.poll(() => analytics.goals().filter((goal) => goal === 'payment_completed').length).toBe(1)
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Доступ активен' })).toBeVisible()
  expect(analytics.goals().filter((goal) => goal === 'payment_completed')).toHaveLength(1)
  expect(JSON.stringify(analytics.calls)).not.toMatch(/PRIVATE_|aaaaaaaa|qa-user|qa@example/)
})

test('an existing paid subscription cannot turn an unpaid or missing checkout into a conversion', async ({ page, context }) => {
  const analytics = await analyticsFixture(context)
  const paymentId = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
  await context.route(`**/api/payments/${paymentId}/status`, route => route.fulfill({
    json: { status: 'pending', paid: false },
  }))
  await context.route('**/api/subscription/status', route => route.fulfill({
    json: { isActive: true, canCreateFreeReading: true, freeReadingsUsedToday: 0, freeReadingsDailyLimit: 1 },
  }))
  await page.goto('/')
  await page.getByTestId('accept-all').click()
  await page.evaluate(({ paymentId, auth }) => {
    localStorage.setItem('fv_token', auth.accessToken)
    localStorage.setItem('fv_email', auth.email)
    localStorage.setItem('fv_user_id', auth.userId)
    localStorage.setItem('fv_pending_payment_v1', JSON.stringify({
      paymentId, userId: auth.userId, expires: Date.now() + 3600000,
    }))
  }, { paymentId, auth: verifiedAuth })
  await page.goto('/payment/success')
  await expect(page.getByRole('heading', { name: 'Платёж в обработке' })).toBeVisible()
  expect(analytics.goals()).not.toContain('payment_completed')
  await page.evaluate(() => localStorage.removeItem('fv_pending_payment_v1'))
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Доступ активен' })).toBeVisible()
  expect(analytics.goals()).not.toContain('payment_completed')
})
