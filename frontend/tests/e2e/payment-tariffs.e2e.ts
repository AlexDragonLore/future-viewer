import { expect, test, type Page } from '@playwright/test'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

test.use({ deviceScaleFactor: 1 })

const products = [
  { tariffCode: 'pro-7d', amount: 99, currency: 'RUB', accessDays: 7 },
  { tariffCode: 'pro-30d', amount: 299, currency: 'RUB', accessDays: 30 },
]
const scenarios = [
  { name: 'blocked spread purchase', path: '/', active: false, button: 'Оплатить доступ' },
  { name: 'active access renewal', path: '/profile', active: true, button: 'Продлить доступ' },
]

async function openTariffs(page: Page, scenario: typeof scenarios[number]) {
  const payments: unknown[] = []
  const unexpectedRequests: string[] = []
  const responses: Record<string, unknown> = {
    '/api/public/config': {
      supportEmail: '', paymentsEnabled: true,
      paymentProduct: products[1], paymentProducts: products,
    },
    '/api/public/legal-documents': legalDocumentsResponse,
    '/api/subscription/status': {
      status: scenario.active ? 1 : 0, isActive: scenario.active,
      expiresAt: scenario.active ? '2030-01-01T00:00:00Z' : null,
      freeReadingsUsedToday: 0, freeReadingsDailyLimit: 1, canCreateIntroReading: false, canCreateFreeReading: true,
    },
    '/api/announcements/unread': [],
    '/api/leaderboard/me': null,
    '/api/feedbacks/my': [],
    '/api/profile/personalization': { firstName: null, lastName: null, birthYear: null, isComplete: false, memoryRules: [] },
    '/api/privacy/settings': {
      historyEnabled: false, ageConfirmed18: true, personalizationEnabled: false,
      marketingEnabled: false, analyticsEnabled: false,
    },
    '/api/privacy/consents': [],
    '/api/privacy/account-deletion/status': { requested: false },
  }
  await page.route('**/api/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (!path.startsWith('/api/')) return route.fallback()
    if (path === '/api/payments/subscribe' && request.method() === 'POST') {
      payments.push(request.postDataJSON())
      // Keep every payment inside the local fixture, without a processor redirect.
      return route.fulfill({ status: 503, json: { message: 'Проверочный платёж: переход отключён.' } })
    }
    if (request.method() !== 'GET' || !(path in responses)) {
      unexpectedRequests.push(`${request.method()} ${path}`)
      return route.fulfill({ status: 400, json: { error: 'Unexpected test request' } })
    }
    return route.fulfill({ json: responses[path] })
  })
  await page.addInitScript(() => {
    localStorage.setItem('fv_token', 'local-tariffs-fixture')
    localStorage.setItem('fv_email', 'tariffs@example.com')
    localStorage.setItem('fv_user_id', 'local-tariffs-user')
  })
  await page.goto(scenario.path)
  await page.getByTestId('accept-necessary').click()
  if (scenario.active) {
    await expect(page.getByTestId('profile-access')).toContainText('Платный доступ активен')
  } else {
    await page.getByRole('button', { name: 'Три карты' }).click()
    await expect(page.locator('.subscription-banner')).toHaveCount(1)
    await expect(page.getByTestId('block-warning')).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Начать расклад', exact: true })).toHaveCount(0)
    await expect(page.locator('.payment-info')).toHaveCount(0)
  }
  await expect(page.locator('.subscription-banner')).toBeVisible()
  await page.evaluate(() => document.fonts.ready)
  return { payments, unexpectedRequests }
}

for (const width of [320, 1280]) {
  for (const scenario of scenarios) {
    for (const product of products) {
      test(`${scenario.name} sends ${product.tariffCode} at ${width}px`, async ({ page }, testInfo) => {
        await page.setViewportSize({ width, height: 900 })
        await page.emulateMedia({ reducedMotion: 'reduce' })
        const { payments, unexpectedRequests } = await openTariffs(page, scenario)
        const banner = page.locator('.subscription-banner')
        const weekly = page.getByTestId('tariff-pro-7d')
        const monthly = page.getByTestId('tariff-pro-30d')
        await expect(weekly).toBeChecked()
        await expect(monthly).not.toBeChecked()
        await expect(banner.locator('.tariff-name')).toHaveText(['Неделя', 'Месяц'])
        await expect(banner.locator('.tariff-option').filter({ hasText: '7 дней' })).toContainText(/99\s*₽/)
        await expect(banner.locator('.tariff-option').filter({ hasText: '30 дней' })).toContainText(/299\s*₽/)
        await expect(banner).toContainText('без автосписаний')

        const button = page.getByRole('button', { name: scenario.button, exact: true })
        await expect(button).toBeDisabled()
        await page.getByTestId(`tariff-${product.tariffCode}`).check()
        await expect(page.getByTestId(`tariff-${product.tariffCode}`)).toBeChecked()
        await expect(button).toBeDisabled()
        expect(payments).toEqual([])

        const acceptance = page.getByTestId('payment-offer-acceptance')
        await expect(acceptance).not.toBeChecked()
        await acceptance.check()
        await expect(button).toBeEnabled()
        await expect(banner.getByRole('link', { name: 'оферты', exact: true })).toHaveAttribute('href', '/legal/offer')

        const overflowingElements = await banner.locator('*').evaluateAll(elements =>
          elements.flatMap(element => {
            const rect = element.getBoundingClientRect()
            return rect.width && (rect.left < -1 || rect.right > document.documentElement.clientWidth + 1)
              ? [{ element: element.getAttribute('data-testid') ?? element.className, left: rect.left, right: rect.right }]
              : []
          }),
        )
        expect(overflowingElements).toEqual([])
        const clippedTariffText = await banner.locator('.tariff-name, .tariff-price, .tariff-period').evaluateAll(elements =>
          elements.filter(element => element.scrollWidth > element.clientWidth + 1).map(element => element.textContent),
        )
        expect(clippedTariffText).toEqual([])
        if (product.tariffCode === 'pro-7d') {
          const context = scenario.active ? page.getByTestId('profile-access') : page.locator('.home-page')
          const screenshot = testInfo.outputPath(`tariffs-context-${width}px.png`)
          if (scenario.active) {
            await context.screenshot({ path: screenshot, scale: 'css' })
          } else {
            await page.evaluate(() => window.scrollTo(0, 0))
            await page.screenshot({ path: screenshot, scale: 'css', fullPage: true })
          }
          await testInfo.attach(`tariffs-context-${width}px`, { path: screenshot, contentType: 'image/png' })
          if (scenario.active && width === 320) {
            await page.setViewportSize({ width: 393, height: 900 })
            const mobileScreenshot = testInfo.outputPath('tariffs-profile-context-393px.png')
            await context.screenshot({ path: mobileScreenshot, scale: 'css' })
            await testInfo.attach('tariffs-profile-context-393px', { path: mobileScreenshot, contentType: 'image/png' })
            await page.setViewportSize({ width, height: 900 })
          }
        }

        await button.click()
        await expect(banner).toContainText('Проверочный платёж: переход отключён.')
        expect(payments).toEqual([{
          tariffCode: product.tariffCode, offerAccepted: true, offerVersion: 'published-offer',
        }])
        await expect(page).toHaveURL(scenario.active ? /\/profile$/ : /\/$/)
        expect(unexpectedRequests).toEqual([])
      })
    }
  }
}
