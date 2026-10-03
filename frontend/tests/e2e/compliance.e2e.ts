import { expect, test } from '@playwright/test'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

const optionalConsentTestIds = [
  'personalization-consent',
  'telegram-consent',
  'marketing-consent',
  'analytics-consent',
]

const legalRoutes = [
  '/legal/privacy',
  '/legal/personal-data-consent',
  '/legal/cookies',
  '/legal/offer',
  '/legal/marketing-consent',
  '/legal/ai-disclaimer',
  '/legal/data-request',
  '/legal/processors',
]

async function acceptNecessary(page: import('@playwright/test').Page) {
  const button = page.getByTestId('accept-necessary')
  if (await button.isVisible()) await button.click()
}

test.beforeEach(async ({ page }) => {
  await page.route('**/api/public/legal-documents', route => route.fulfill({ json: legalDocumentsResponse }))
  await page.goto('/')
  await page.evaluate(() => localStorage.clear())
  await page.reload()
})

test('registration keeps a single required checkbox and has no publication blocker', async ({ page }) => {
  await acceptNecessary(page)
  await page.goto('/auth')
  await page.getByRole('button', { name: 'Создать аккаунт' }).click()

  await expect(page.getByTestId('registration-legal-blocked')).toHaveCount(0)
  await expect(page.locator('form input[type="checkbox"]')).toHaveCount(1)
  await expect(page.getByTestId('personal-data-consent-acceptance')).not.toBeChecked()
  await expect(page.getByTestId('registration-terms')).toContainText('возраст 18+')
  await expect(
    page.getByTestId('personal-data-consent-acceptance').locator('xpath=following-sibling::span//a'),
  ).toHaveAttribute('href', '/legal/personal-data-consent')
  for (const testId of optionalConsentTestIds) {
    await expect(page.getByTestId(testId)).toHaveCount(0)
  }
  await expect(page.getByRole('button', { name: 'Создать', exact: true })).toBeDisabled()
  await page.getByTestId('personal-data-consent-acceptance').check()
  await expect(page.getByRole('button', { name: 'Создать', exact: true })).toBeEnabled()
})

test('optional browser storage stays off before a choice and necessary-only is persisted', async ({ page }) => {
  const thirdPartyRequests: string[] = []
  page.on('request', request => {
    if (/googletagmanager|google-analytics|mc\.yandex|sentry|posthog/i.test(request.url())) {
      thirdPartyRequests.push(request.url())
    }
  })

  await page.getByRole('button', { name: 'Детальная настройка' }).click()
  await expect(page.getByTestId('cookie-preferences-toggle')).not.toBeChecked()
  await expect(page.getByTestId('cookie-analytics-toggle')).not.toBeChecked()
  await expect(page.getByTestId('cookie-marketing-toggle')).not.toBeChecked()
  await page.getByTestId('accept-necessary').click()

  const stored = await page.evaluate(() => localStorage.getItem('fv_cookie_preferences_v1'))
  expect(stored).toContain('"analytics":false')
  expect(stored).toContain('"marketing":false')
  expect(thirdPartyRequests).toEqual([])
})

test('question containing an email is blocked locally and never starts the reading request', async ({ page }) => {
  await acceptNecessary(page)
  let readingRequestCount = 0
  page.on('request', request => {
    if (/\/api\/readings(?:\/(?:stream|guest))?$/.test(new URL(request.url()).pathname)) readingRequestCount++
  })

  await page.getByPlaceholder('На что мне сейчас стоит обратить внимание?').fill('Напиши мне на ivan.petrov@example.com')
  await page.getByRole('button', { name: 'Открыть карту бесплатно' }).click()

  await expect(page.getByTestId('question-validation')).toContainText('персональ')
  expect(readingRequestCount).toBe(0)
  await expect(page).toHaveURL(/\/$/)
})

test('home defaults history off and displays the exact AI/Tarot disclaimer', async ({ page }) => {
  await acceptNecessary(page)
  await expect(page.getByTestId('save-to-history')).toHaveCount(0)
  await expect(page.getByTestId('ai-disclaimer')).toContainText('развлекательную и информационную интерпретацию карт')
  await expect(page.getByTestId('ai-disclaimer')).toContainText('не является достоверным предсказанием')
})

test('all legal routes publish their actual text and remain noindex', async ({ page }) => {
  await acceptNecessary(page)
  const forbidden = /TODO|указать адрес|указать провайдера|\[ФИО\]|\[ИНН\]|example\.com/i

  for (const route of legalRoutes) {
    await page.goto(route)
    expect(await page.locator('main').innerText()).not.toMatch(forbidden)
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/i)
    await expect(page.getByTestId('legal-document')).toBeVisible()
  }
})
