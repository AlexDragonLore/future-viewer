import { expect, test, type Locator, type Page } from '@playwright/test'
import {
  adminFeedbackFixtures,
  adminFixtureResponses,
  adminFixtureSession,
  adminReadingFixtures,
  adminUserDetailFixture,
} from '../fixtures/admin'

async function openAdmin(page: Page, path = '/admin') {
  const requests = { readings: [] as { page: number; search: string }[], answers: [] as string[], unexpected: [] as string[] }
  await page.route('**/api/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    if (!url.pathname.startsWith('/api/')) return route.fallback()
    if (url.pathname.startsWith('/api/admin/')) {
      expect(request.headers().authorization).toBe(`Bearer ${adminFixtureSession.token}`)
    }
    if (request.method() !== 'GET') {
      requests.unexpected.push(`${request.method()} ${url.pathname}`)
      return route.fulfill({ status: 405, json: { message: 'Browser QA does not mutate data.' } })
    }
    if (url.pathname === '/api/admin/readings') {
      expect(url.searchParams.get('includeInterpretation')).toBe('false')
      const pageNumber = Number(url.searchParams.get('page') ?? 1)
      const pageSize = Number(url.searchParams.get('pageSize') ?? 20)
      const search = url.searchParams.get('search') ?? ''
      const userId = url.searchParams.get('userId')
      requests.readings.push({ page: pageNumber, search })
      const rows = adminReadingFixtures.filter(reading =>
        (!userId || reading.userId === userId)
        && `${reading.userEmail} ${reading.question}`.toLowerCase().includes(search.toLowerCase()),
      )
      if (pageNumber === 2) await new Promise(resolve => setTimeout(resolve, 300))
      return route.fulfill({ json: {
        items: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize).map(({ interpretation: _answer, ...reading }) => reading), total: rows.length,
      } })
    }
    if (/^\/api\/admin\/readings\/[^/]+\/interpretation$/.test(url.pathname)) {
      const id = url.pathname.split('/')[4]
      requests.answers.push(id)
      const reading = adminReadingFixtures.find(row => row.id === id)
      return route.fulfill({ status: reading ? 200 : 404, json: { id, interpretation: reading?.interpretation } })
    }
    if (url.pathname in adminFixtureResponses) {
      return route.fulfill({ json: adminFixtureResponses[url.pathname] })
    }
    requests.unexpected.push(`${request.method()} ${url.pathname}`)
    return route.fulfill({ status: 404, json: { message: 'Unexpected test request' } })
  })
  await page.addInitScript(session => {
    localStorage.setItem('fv_token', session.token)
    localStorage.setItem('fv_email', session.email)
    localStorage.setItem('fv_user_id', session.userId)
    localStorage.setItem('fv_is_admin', String(session.isAdmin))
  }, adminFixtureSession)
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.goto(path)
  await page.getByTestId('accept-necessary').click()
  await expect(page.getByTestId('admin-view')).toBeVisible()
  await page.evaluate(() => document.fonts.ready)
  return requests
}

async function expectContained(root: Locator) {
  const overflowing = await root.locator('*').evaluateAll(elements => elements.flatMap(element => {
    const bounds = element.getBoundingClientRect()
    if (!bounds.width || !bounds.height || getComputedStyle(element).visibility === 'hidden') return []
    return bounds.left < -1 || bounds.right > document.documentElement.clientWidth + 1
      ? [element.getAttribute('data-testid') ?? element.className]
      : []
  }))
  expect(overflowing).toEqual([])
}

async function expectFullText(root: Locator, text: string) {
  const paragraph = root.getByText(text, { exact: true })
  await expect(paragraph).toBeVisible()
  expect(await paragraph.evaluate(element =>
    element.scrollHeight <= element.clientHeight + 1 && element.scrollWidth <= element.clientWidth + 1,
  )).toBe(true)
}

test('latest messages show full content, hidden status, user details and reset pagination on search', async ({ page }, testInfo) => {
  const requests = await openAdmin(page)
  await expect(page).toHaveURL(/\/admin\/readings$/)
  const rows = page.getByTestId('admin-reading-row')
  const newest = rows.first()
  await expect(newest).toContainText('Как решиться на смену работы')
  await expect(rows.nth(1)).toContainText('Скрыт из истории')
  expect(requests.answers).toEqual([])
  await expect(page.getByTestId('admin-reading-answer')).toHaveCount(0)
  await expect(newest.getByTestId('admin-subscription-purchase')).toContainText('PRO · 30 дней')
  await expect(newest.getByTestId('admin-subscription-purchase')).toContainText('499')
  const collapsedHeight = (await newest.boundingBox())!.height
  expect(collapsedHeight).toBeLessThan(page.viewportSize()!.width < 640 ? 260 : 170)
  await newest.getByTestId('admin-question-expand').click()
  await newest.getByTestId('admin-reading-expand').click()
  await expectFullText(newest, adminReadingFixtures[0].question)
  await expect(newest.getByText('Финальный совет: запиши один конкретный шаг на завтра.')).toBeVisible()
  expect(requests.answers).toEqual([adminReadingFixtures[0].id])
  await expectContained(page.getByTestId('admin-view'))
  await newest.screenshot({ path: testInfo.outputPath('admin-reading-expanded.png') })

  const userButton = newest.getByTestId('admin-reading-user')
  await userButton.click()
  const drawer = page.getByTestId('admin-user-drawer')
  await expect(drawer).toBeVisible()
  await expect(drawer).toContainText(adminUserDetailFixture.email)
  await expect(drawer).toContainText('Как решиться на смену работы')
  await expectContained(drawer)
  await drawer.screenshot({ path: testInfo.outputPath('admin-user-drawer.png') })
  await page.getByTestId('admin-user-drawer-close').click()
  await expect(drawer).toHaveCount(0)
  await expect(userButton).toBeFocused()

  await page.getByTestId('admin-reading-next').click()
  await expect(rows).toHaveCount(20)
  await expect(page.getByRole('status').filter({ hasText: 'Загружаю страницу 2' })).toBeVisible()
  await expect(rows).toHaveCount(1)
  await expect(rows.first()).toContainText('Архивный вопрос 21')
  expect(requests.readings.at(-1)?.page).toBe(2)
  const beforeBack = requests.readings.length
  await page.getByTestId('admin-reading-prev').click()
  await expect(rows).toHaveCount(20)
  expect(requests.readings).toHaveLength(beforeBack)
  await page.getByTestId('admin-reading-search').fill('смену работы')
  await page.getByTestId('admin-reading-search').press('Enter')
  await expect(rows).toHaveCount(1)
  await expect(rows.first()).toContainText('Как решиться на смену работы')
  expect(requests.readings.at(-1)).toEqual({ page: 1, search: 'смену работы' })
  await expect(page.getByTestId('admin-reading-next')).toBeDisabled()
  await expectContained(page.getByTestId('admin-view'))
  expect(requests.unexpected).toEqual([])
})

test('feedback exposes complete question, response and scoring reason without horizontal overflow', async ({ page }, testInfo) => {
  const requests = await openAdmin(page, '/admin/feedbacks')
  const details = page.locator('[data-testid="admin-feedback-content"]:visible').first()
  await expect(details).toBeVisible()
  await details.locator('summary').click()
  await expectFullText(details, adminFeedbackFixtures[0].question)
  await expectFullText(details, adminFeedbackFixtures[0].selfReport)
  await expectFullText(details, adminFeedbackFixtures[0].aiScoreReason)
  await expectContained(page.getByTestId('admin-view'))
  await page.screenshot({ path: testInfo.outputPath('admin-feedback-expanded.png'), fullPage: true })
  expect(requests.unexpected).toEqual([])
})


test('users show actual purchases on desktop and mobile and in the detail drawer', async ({ page }) => {
  const requests = await openAdmin(page, '/admin/users')
  const root = page.getByTestId('admin-users-view')
  const purchase = root.locator('[data-testid="admin-subscription-purchase"]:visible')
  await expect(purchase.first()).toContainText('PRO · 30 дней')
  await expect(purchase.first()).toContainText('499')
  await expectContained(root)
  const user = page.viewportSize()!.width < 769
    ? page.getByTestId('admin-user-card-button').first()
    : page.getByTestId('admin-user-row').first().getByRole('button')
  await user.click()
  const drawer = page.getByTestId('admin-user-drawer')
  await expect(drawer.getByTestId('admin-subscription-purchase')).toContainText('499')
  await expect(drawer.getByTestId('admin-subscription-status')).toContainText('Доступ активен')
  await expectContained(drawer)
  expect(requests.unexpected).toEqual([])
})
