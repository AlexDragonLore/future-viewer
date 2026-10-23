import { expect, test, type Page } from '@playwright/test'
import { adminFixtureResponses, adminFixtureSession, adminUserDetailFixture } from '../fixtures/admin'
import { adminPurchaseFixtures } from '../fixtures/adminPurchases'

async function openHistory(page: Page, failFirst = false) {
  const queries: URLSearchParams[] = []
  const unexpected: string[] = []
  await page.route('**/api/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    if (!url.pathname.startsWith('/api/')) return route.fallback()
    if (url.pathname.startsWith('/api/admin/')) expect(request.headers().authorization).toBe('Bearer ' + adminFixtureSession.token)
    if (request.method() !== 'GET') {
      unexpected.push(request.method() + ' ' + url.pathname)
      return route.fulfill({ status: 405 })
    }
    if (url.pathname === '/api/admin/purchases') {
      queries.push(url.searchParams)
      if (failFirst) {
        failFirst = false
        return route.fulfill({ status: 500, json: { message: 'Не удалось загрузить историю покупок' } })
      }
      const status = url.searchParams.get('status') || 'paid'
      const user = url.searchParams.get('userId')
      const search = (url.searchParams.get('search') || '').toLowerCase()
      const rows = adminPurchaseFixtures.filter(p =>
        (status === 'all' || p.status === status) && (!user || p.userId === user)
        && [p.userEmail, p.publicId, p.providerPaymentId].some(value => value?.toLowerCase().includes(search)))
      const pageNumber = Number(url.searchParams.get('page'))
      const pageSize = Number(url.searchParams.get('pageSize'))
      return route.fulfill({ json: { items: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize), total: rows.length } })
    }
    if (url.pathname in adminFixtureResponses) return route.fulfill({ json: adminFixtureResponses[url.pathname] })
    unexpected.push(url.pathname)
    return route.fulfill({ status: 404 })
  })
  await page.addInitScript(session => {
    localStorage.setItem('fv_token', session.token)
    localStorage.setItem('fv_email', session.email)
    localStorage.setItem('fv_user_id', session.userId)
    localStorage.setItem('fv_is_admin', 'true')
  }, adminFixtureSession)
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.goto('/admin/purchases')
  await page.getByTestId('accept-necessary').click()
  await expect(page.getByTestId('admin-purchases-view')).toBeVisible()
  return { queries, unexpected }
}

async function expectContained(page: Page) {
  const overflow = await page.getByTestId('admin-view').locator('*').evaluateAll(elements => elements.filter(element => {
    const rect = element.getBoundingClientRect()
    return rect.width > 0 && rect.height > 0 && (rect.left < -1 || rect.right > document.documentElement.clientWidth + 1)
  }).map(element => element.outerHTML.slice(0, 150)))
  expect(overflow).toEqual([])
}

test('paid history, pagination, payment details, filters and user history', async ({ page }, info) => {
  const requests = await openHistory(page)
  const rows = page.getByTestId('admin-purchase-row')
  await expect(rows).toHaveCount(20)
  await expect(page.getByTestId('admin-purchases-total')).toHaveText('Всего: 21')
  await expect(rows.first()).toContainText('299')
  await expect(rows.nth(1)).toContainText('Пользователь удалён')
  await rows.first().getByText('Детали платежа').click()
  await expect(rows.first()).toContainText(adminPurchaseFixtures[0].providerPaymentId!)
  await expectContained(page)
  await page.screenshot({ path: info.outputPath('purchase-history.png'), fullPage: false })
  await page.getByRole('button', { name: 'Следующая страница покупок' }).click()
  await expect(rows).toHaveCount(1)
  expect(requests.queries.at(-1)?.get('page')).toBe('2')

  await page.getByTestId('admin-purchase-status').selectOption('refunded')
  await expect(rows).toHaveCount(1)
  await expect(rows.first()).toContainText('Возврат')
  expect(requests.queries.at(-1)?.get('page')).toBe('1')
  await page.getByTestId('admin-purchase-status').selectOption('all')
  await page.getByTestId('admin-purchase-search').fill(adminPurchaseFixtures[25].publicId)
  await page.getByTestId('admin-purchase-search').press('Enter')
  await expect(rows).toHaveCount(1)
  await expect(rows.first()).toContainText('Ожидает оплаты')
  await rows.first().getByText('Детали платежа').click()
  await expect(rows.first()).toContainText('Ещё не присвоен')
  await expect(rows.first()).toContainText('Не подтверждена')
  await page.getByTestId('admin-purchase-search').fill('absent@example.com')
  await page.getByTestId('admin-purchase-search').press('Enter')
  await expect(page.getByTestId('admin-purchases-empty')).toBeVisible()
  await page.getByRole('button', { name: 'Сбросить', exact: true }).click()
  await expect(rows).toHaveCount(20)
  await rows.first().getByRole('link').click()
  await expect(page.getByTestId('admin-user-drawer')).toBeVisible()
  await page.getByTestId('admin-user-purchases').click()
  await expect(page).toHaveURL(new RegExp('/admin/purchases\\?userId=' + adminUserDetailFixture.id))
  await expect(page.getByTestId('admin-user-drawer')).toHaveCount(0)
  await expect(page.getByTestId('admin-purchase-user-filter')).toBeVisible()
  await expect(page.getByTestId('admin-purchases-total')).toHaveText('Всего: 20')
  expect(requests.queries.at(-1)?.get('userId')).toBe(adminUserDetailFixture.id)
  await page.getByRole('link', { name: 'Все пользователи', exact: true }).click()
  await expect(page.getByTestId('admin-purchases-total')).toHaveText('Всего: 21')
  await expectContained(page)
  expect(requests.unexpected).toEqual([])
})

test('failed request can be retried', async ({ page }) => {
  await openHistory(page, true)
  await expect(page.getByRole('alert')).toContainText('Не удалось загрузить историю покупок')
  await expect(page.getByTestId('admin-purchase-row')).toHaveCount(0)
  await page.getByRole('button', { name: 'Повторить', exact: true }).click()
  await expect(page.getByTestId('admin-purchase-row')).toHaveCount(20)
})

test('purchase history fits narrow screens and exposes all statuses', async ({ page }, info) => {
  await page.setViewportSize({ width: 320, height: 844 })
  await openHistory(page)
  await page.getByTestId('admin-purchase-status').selectOption('all')
  await page.getByRole('button', { name: 'Следующая страница покупок' }).click()
  await expect(page.getByTestId('admin-purchase-row')).toHaveCount(6)
  for (const text of ['Оплачено', 'Возврат', 'Отменено', 'Ошибка оплаты', 'Ожидает оплаты']) {
    await expect(page.getByTestId('admin-purchases-view')).toContainText(text)
  }
  await page.getByTestId('admin-purchase-row').last().getByText('Детали платежа').click()
  await expectContained(page)
  await page.screenshot({ path: info.outputPath('purchase-history-320.png'), fullPage: true })
})
