import { expect, test, type Page } from '@playwright/test'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

async function openHome(page: Page, options: { authenticated?: boolean; active?: boolean; exhausted?: boolean } = {}) {
  await page.setViewportSize({ width: 393, height: 900 })
  await page.emulateMedia({ reducedMotion: 'reduce' })
  const mutations: string[] = []
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
    '/api/announcements/unread': [],
    '/api/subscription/status': {
      status: options.active ? 1 : 0, isActive: Boolean(options.active),
      expiresAt: options.active ? '2030-01-01T00:00:00Z' : null,
      freeReadingsUsedToday: options.exhausted ? 1 : 0,
      freeReadingsDailyLimit: 1, canCreateFreeReading: !options.exhausted,
    },
  }
  await page.route('**/api/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (!path.startsWith('/api/')) return route.fallback()
    if (request.method() !== 'GET') {
      mutations.push(`${request.method()} ${path}`)
      return route.fulfill({ status: 400, json: { message: 'No mutations in this UI test' } })
    }
    if (!(path in responses)) return route.fulfill({ status: 404, json: { message: 'Unexpected fixture request' } })
    return route.fulfill({ json: responses[path] })
  })
  if (options.authenticated) {
    await page.addInitScript(() => {
      localStorage.setItem('fv_token', 'local-paid-access-fixture')
      localStorage.setItem('fv_email', 'mobile@example.com')
      localStorage.setItem('fv_user_id', 'local-paid-access-user')
    })
  }
  await page.goto('/')
  await page.getByTestId('accept-necessary').click()
  if (options.authenticated) {
    await expect(page.locator('.subscription-badge')).toContainText(options.active ? 'Доступ активен' : 'Бесплатно сегодня')
    await page.getByRole('textbox', { name: 'Вопрос', exact: true }).fill('Какой следующий шаг мне подходит?')
  }
  await page.evaluate(() => document.fonts.ready)
  return mutations
}

async function expectSinglePaymentAction(page: Page, message: string) {
  await expect(page.locator('.subscription-banner')).toHaveCount(1)
  await expect(page.locator('.subscription-banner')).toContainText(message)
  await expect(page.locator('.subscription-banner .tariff-option').filter({ hasText: '7 дней' })).toContainText(/99\s*₽/)
  await expect(page.locator('.subscription-banner .tariff-option').filter({ hasText: '30 дней' })).toContainText(/299\s*₽/)
  await expect(page.getByRole('button', { name: 'Начать расклад', exact: true })).toHaveCount(0)
  await expect(page.getByTestId('block-warning')).toHaveCount(0)
  await expect(page.locator('.payment-info')).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Оплатить доступ', exact: true })).toBeDisabled()
  const outsideViewport = await page.locator('.home-page, .home-page *').evaluateAll(elements => elements.flatMap(element => {
    const rect = element.getBoundingClientRect()
    if (!rect.width || !rect.height) return []
    return rect.left < -1 || rect.right > document.documentElement.clientWidth + 1 ? [element.className] : []
  }))
  expect(outsideViewport).toEqual([])
}

test('mobile paid spread shows one payment action and changing back restores Start', async ({ page }, testInfo) => {
  const mutations = await openHome(page, { authenticated: true })
  await page.locator('.spread-option').nth(1).click()
  await expectSinglePaymentAction(page, 'Открой все расклады')
  await page.locator('.subscription-banner').scrollIntoViewIfNeeded()
  await page.screenshot({ path: testInfo.outputPath('paid-spread-mobile.png') })
  await page.getByTestId('payment-offer-acceptance').check()
  await expect(page.getByRole('button', { name: 'Оплатить доступ', exact: true })).toBeEnabled()

  await page.locator('.spread-option').first().click()
  await expect(page.locator('.subscription-banner')).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Начать расклад', exact: true })).toBeEnabled()
  expect(mutations).toEqual([])
})

test('mobile exhausted daily quota shows only its payment banner', async ({ page }, testInfo) => {
  const mutations = await openHome(page, { authenticated: true, exhausted: true })
  await expectSinglePaymentAction(page, 'Расклады без ограничений')
  await page.locator('.subscription-banner').scrollIntoViewIfNeeded()
  await page.screenshot({ path: testInfo.outputPath('exhausted-quota-mobile.png') })
  expect(mutations).toEqual([])
})

test('subscriber can start a multi-card reading without redundant payment text', async ({ page }, testInfo) => {
  const mutations = await openHome(page, { authenticated: true, active: true, exhausted: true })
  await page.locator('.spread-option').nth(2).click()
  await expect(page.getByRole('button', { name: 'Начать расклад', exact: true })).toBeEnabled()
  await expect(page.locator('.subscription-banner, .payment-info')).toHaveCount(0)
  await expect(page.getByTestId('block-warning')).toHaveCount(0)
  await page.getByRole('button', { name: 'Начать расклад', exact: true }).scrollIntoViewIfNeeded()
  await page.screenshot({ path: testInfo.outputPath('subscriber-start-mobile.png') })
  expect(mutations).toEqual([])
})

test('guest still sees the compact paid option under the free card button', async ({ page }, testInfo) => {
  const mutations = await openHome(page)
  await expect(page.getByRole('button', { name: 'Открыть карту бесплатно', exact: true })).toBeEnabled()
  await expect(page.getByTestId('guest-paid-offer')).toContainText('Все 3 расклада безлимитно')
  await expect(page.getByTestId('guest-paid-offer')).toContainText(/99\s*₽/)
  await expect(page.getByTestId('guest-paid-offer')).toContainText('7 дней')
  await expect(page.getByTestId('guest-paid-offer')).toContainText(/299\s*₽/)
  await expect(page.getByTestId('guest-paid-offer')).toContainText('30 дней')
  await expect(page.locator('.subscription-banner, .payment-info')).toHaveCount(0)
  await expect(page.getByTestId('payment-offer-acceptance')).toHaveCount(0)
  await page.getByTestId('guest-paid-offer').scrollIntoViewIfNeeded()
  await page.screenshot({ path: testInfo.outputPath('guest-compact-offer-mobile.png') })
  expect(mutations).toEqual([])
})
