import { expect, test, type BrowserContext, type Page } from '@playwright/test'
import { fullReading, guestResponse, previewReading, verifiedAuth } from './guestReading.fixture'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

async function mockApi(context: BrowserContext) {
  const calls = { create: 0, unlock: 0 }
  await context.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname
    if (!path.startsWith('/api/')) return route.fallback()
    let body: unknown = {}
    switch (path) {
      case '/api/public/config': body = { supportEmail: '', paymentsEnabled: false }; break
      case '/api/public/legal-documents': body = legalDocumentsResponse; break
      case '/api/auth/register':
        expect(route.request().postDataJSON()).toMatchObject({ personalDataConsentAccepted: true, optionalConsents: { personalization: false, marketing: false, analytics: false } })
        await route.fulfill({ status: 202, json: { userId: '00000000-0000-0000-0000-000000000000', email: 'qa@example.com', verificationRequired: true } })
        return
      case '/api/readings/guest':
        calls.create++
        expect(route.request().postDataJSON()).toMatchObject({ spreadType: 1, question: previewReading.question })
        body = guestResponse()
        break
      case '/api/readings/guest/preview': body = previewReading; break
      case '/api/readings/guest/unlock':
        expect(route.request().headers().authorization).toBe('Bearer qa-verified-session')
        calls.unlock++
        body = fullReading
        break
      case '/api/auth/verify-email':
      case '/api/auth/login': body = verifiedAuth; break
      case '/api/announcements/unread': body = []; break
      case '/api/subscription/status': body = { isActive: false, canCreateFreeReading: false, freeReadingsUsedToday: 1, freeReadingsDailyLimit: 1 }; break
      default:
        await route.fulfill({ status: 404, json: { message: 'Unexpected test request' } })
        return
    }
    await route.fulfill({ json: body })
  })
  return calls
}

async function openGuestCard(page: Page) {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Открой свою карту' })).toBeVisible()
  const necessary = page.getByTestId('accept-necessary')
  if (await necessary.isVisible()) await necessary.click()
  await expect(page.getByRole('textbox')).toHaveValue('')
  await page.getByRole('button', { name: 'Открыть карту бесплатно' }).click()
  await expect(page).toHaveURL(/\/result$/, { timeout: 15000 })
  await expect(page.getByTestId('guest-unlock')).toBeVisible({ timeout: 10000 })
}

test('one-click guest preview survives reload and email verification in a new tab', async ({ page, context }, testInfo) => {
  test.setTimeout(60000)
  const calls = await mockApi(context)
  await openGuestCard(page)
  await expect(page.locator('.card-entry')).toHaveCount(1)
  await expect(page.getByRole('img', { name: 'Солнце' })).toBeVisible()
  await expect(page.locator('.prose-mystic')).not.toContainText('Следующий шаг')
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
  await page.getByTestId('guest-unlock').scrollIntoViewIfNeeded()
  await page.screenshot({ path: testInfo.outputPath('guest-preview.png') })

  await page.reload()
  await expect(page.getByTestId('guest-unlock')).toBeVisible({ timeout: 10000 })
  expect(calls.create).toBe(1)
  const persisted = await page.evaluate(() => localStorage.getItem('fv_guest_reading_v1'))
  expect(persisted).toContain('qa-encrypted-ticket')
  expect(persisted).not.toContain(previewReading.question)

  await page.getByRole('link', { name: 'Зарегистрироваться и дочитать' }).click()
  await expect(page.getByRole('heading', { name: 'Регистрация', exact: true })).toBeVisible()
  await expect(page.getByTestId('auth-continue-reading')).toContainText('дочитать толкование бесплатно')
  await page.getByRole('textbox', { name: 'Электронная почта' }).fill('qa@example.com')
  await page.getByLabel('Пароль', { exact: true }).fill('test-password123')
  await page.getByTestId('personal-data-consent-acceptance').check()
  await page.getByRole('button', { name: 'Создать', exact: true }).click()
  await expect(page.getByText('Мы отправили письмо на qa@example.com.', { exact: false })).toBeVisible()
  const emailTab = await context.newPage()
  await emailTab.goto('/verify-email#token=qa-email-token')
  await expect(emailTab).toHaveURL(/\/result$/, { timeout: 10000 })
  await expect(emailTab.getByRole('heading', { name: 'Следующий шаг' })).toBeVisible({ timeout: 15000 })
  await expect(emailTab.getByTestId('guest-unlock')).toHaveCount(0)
  await expect(emailTab.getByRole('img', { name: 'Солнце' })).toBeVisible()
  expect(calls.create).toBe(1)
  expect(calls.unlock).toBeGreaterThanOrEqual(1)
})

test('existing account can log in to unlock the same card', async ({ page, context }) => {
  const calls = await mockApi(context)
  await openGuestCard(page)
  await page.getByRole('link', { name: 'Уже есть аккаунт? Войти' }).click()
  await page.getByRole('textbox', { name: 'Электронная почта' }).fill('qa@example.com')
  await page.getByLabel('Пароль', { exact: true }).fill('test-password123')
  await page.getByRole('button', { name: 'Войти', exact: true }).click()
  await expect(page).toHaveURL(/\/result$/)
  await expect(page.getByRole('heading', { name: 'Следующий шаг' })).toBeVisible({ timeout: 15000 })
  expect(calls.create).toBe(1)
  expect(calls.unlock).toBe(1)
})

test('guest API errors return to a usable home instead of authentication', async ({ page, context }) => {
  await mockApi(context)
  await context.route('**/api/readings/guest', route => route.fulfill({ status: 429, json: { message: 'Слишком много запросов. Повторите позже.' } }))
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Открой свою карту' })).toBeVisible()
  const necessary = page.getByTestId('accept-necessary')
  if (await necessary.isVisible()) await necessary.click()
  await page.getByRole('button', { name: 'Открыть карту бесплатно' }).click()
  await expect(page.getByTestId('question-validation')).toContainText('Слишком много запросов')
  await expect(page).toHaveURL(/\/$/)
  await expect(page.getByRole('button', { name: 'Открыть карту бесплатно' })).toBeEnabled()
})
