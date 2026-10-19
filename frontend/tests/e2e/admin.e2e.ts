import type { AdminReading } from '@/types/admin'
import { installPaidAccessFixture } from './paidAccess.fixture'
import { expect, test, type Locator, type Page } from '@playwright/test'
import {
  adminAudienceReadingFixtures,
  adminFixtureResponses,
  adminFixtureSession,
  adminReadingFixtures,
  adminUserDetailFixture,
} from '../fixtures/admin'

async function openAdmin(page: Page, path = '/admin', sourceRows: readonly AdminReading[] = adminReadingFixtures, holdGuestResponse = false) {
  const viewed = new Set<string>()
  const viewedBatches: string[][] = []
  let releaseGuests!: () => void
  const guestResponse = new Promise<void>(resolve => { releaseGuests = resolve })
  const requests = { viewed, viewedBatches, releaseGuests, readings: [] as { page: number; search: string; audience: string }[], answers: [] as string[], unexpected: [] as string[] }
  await page.route('**/api/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    if (!url.pathname.startsWith('/api/')) return route.fallback()
    if (url.pathname.startsWith('/api/admin/')) {
      expect(request.headers().authorization).toBe(`Bearer ${adminFixtureSession.token}`)
    }
    if (url.pathname === '/api/admin/readings/viewed' && request.method() === 'POST') {
      const ids = request.postDataJSON().readingIds as string[]
      viewedBatches.push(ids)
      ids.forEach(id => viewed.add(id))
      return route.fulfill({ status: 204 })
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
      const audience = url.searchParams.get('audience') ?? 'all'
      requests.readings.push({ page: pageNumber, search, audience })
      const rows = sourceRows.filter(reading =>
        (!userId || reading.userId === userId)
        && (url.searchParams.get('unseenOnly') !== 'true' || !viewed.has(reading.id))
        && (audience === 'all' || audience === 'subscribers' && reading.subscription?.isActive
          || audience === 'registered' && reading.userId !== null || audience === 'guests' && reading.userId === null)
        && `${reading.userEmail} ${reading.question}`.toLowerCase().includes(search.toLowerCase()),
      )
      if (url.searchParams.get('sort') === 'oldest') rows.reverse()
      if (audience === 'guests' && holdGuestResponse) await guestResponse
      else if (pageNumber === 2 || audience === 'guests') await new Promise(resolve => setTimeout(resolve, 300))
      return route.fulfill({ json: {
        items: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize).map(({ interpretation: _answer, ...reading }) => reading), total: rows.length,
      } })
    }
    if (/^\/api\/admin\/readings\/[^/]+\/interpretation$/.test(url.pathname)) {
      const id = url.pathname.split('/')[4]
      requests.answers.push(id)
      const reading = sourceRows.find(row => row.id === id)
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

async function showAllNewest(page: Page) {
  await page.getByTestId('admin-reading-seen-all').click()
  await page.getByTestId('admin-reading-sort').selectOption('newest')
  await expect(page.getByTestId('admin-readings-refresh')).toBeEnabled()
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
  await showAllNewest(page)
  await expect(page).toHaveURL(/\/admin\/readings$/)
  const rows = page.getByTestId('admin-reading-row')
  const newest = rows.first()
  await expect(newest).toContainText('Как решиться на смену работы')
  await expect(rows.nth(1).getByTestId('admin-reading-hidden')).toHaveAttribute('aria-label', 'Скрыт из истории пользователем')
  expect(requests.answers).toEqual([])
  await expect(page.getByTestId('admin-reading-answer')).toHaveCount(0)
  await expect(newest.getByTestId('admin-subscription-purchase')).toHaveText('30д')
  await expect(newest.getByTestId('admin-subscription-button')).toHaveAttribute('aria-label', /499/)
  await expect(newest.getByTestId('admin-reading-expand')).toHaveAccessibleName('Показать ответ ИИ')
  await expect(newest.getByTestId('admin-question-expand')).toHaveCount(0)
  await expectFullText(newest, adminReadingFixtures[0].question)
  await expect(page.getByTestId('admin-readings-view')).not.toContainText('Без подписки')
  await expect(page.getByTestId('admin-readings-view')).not.toContainText('Вопросы пользователей и гостей')
  await page.screenshot({ path: testInfo.outputPath('admin-reading-full-question.png'), fullPage: false })
  await newest.getByTestId('admin-reading-expand').click()
  await expectFullText(newest, adminReadingFixtures[0].question)
  await expect(newest.getByText('Финальный совет: запиши один конкретный шаг на завтра.')).toBeVisible()
  expect(requests.answers).toEqual([adminReadingFixtures[0].id])
  await expectContained(page.getByTestId('admin-view'))
  await newest.screenshot({ path: testInfo.outputPath('admin-reading-expanded.png') })

  const userButton = newest.getByTestId('admin-subscription-button')
  await userButton.click()
  const drawer = page.getByTestId('admin-user-drawer')
  await expect(drawer).toBeVisible()
  await expect(drawer).toContainText(adminUserDetailFixture.email)
  await expect(drawer).toContainText('Как решиться на смену работы')
  await expect(drawer).not.toContainText(/Последние отзывы|Отзывов:|Баллов:/)
  await expectContained(drawer)
  await drawer.screenshot({ path: testInfo.outputPath('admin-user-drawer.png') })
  await page.getByTestId('admin-user-drawer-close').click()
  await expect(drawer).toHaveCount(0)
  await expect(userButton).toBeFocused()

  await page.getByTestId('admin-reading-next').click()
  await expect(rows).toHaveCount(20)
  await expect(page.getByRole('status').filter({ hasText: 'Загружаю страницу 2' })).toBeVisible()
  await expect(rows.first()).toContainText('Архивный вопрос 21')
  await expect(rows).toHaveCount(20)
  const firstAfterNext = await rows.first().boundingBox()
  expect(firstAfterNext!.y).toBeGreaterThanOrEqual(75)
  expect(firstAfterNext!.y).toBeLessThanOrEqual(90)
  expect(requests.readings.at(-1)?.page).toBe(2)
  const beforeBack = requests.readings.length
  await page.getByTestId('admin-reading-prev').click()
  await expect(rows.first()).toContainText('Как решиться на смену работы')
  await expect(rows).toHaveCount(20)
  const firstAfterBack = await rows.first().boundingBox()
  expect(firstAfterBack!.y).toBeGreaterThanOrEqual(75)
  expect(firstAfterBack!.y).toBeLessThanOrEqual(90)
  expect(requests.readings).toHaveLength(beforeBack)
  await page.getByTestId('admin-reading-search').fill('смену работы')
  await page.getByTestId('admin-reading-search').press('Enter')
  await expect(rows).toHaveCount(1)
  await expect(rows.first()).toContainText('Как решиться на смену работы')
  expect(requests.readings.at(-1)).toEqual({ page: 1, search: 'смену работы', audience: 'all' })
  await expect(page.getByTestId('admin-reading-next')).toBeDisabled()
  await expectContained(page.getByTestId('admin-view'))
  expect(requests.unexpected).toEqual([])
})

test('retired feedback admin link opens messages and remaining sections contain no feedback metrics', async ({ page }, testInfo) => {
  const requests = await openAdmin(page, '/admin/feedbacks?token=legacy-secret#legacy-secret')
  await expect(page).toHaveURL(/\/admin\/readings$/)
  await expect(page.getByTestId('admin-tab-feedbacks')).toHaveCount(0)
  await page.getByTestId('admin-tab-users').click()
  await expect(page.getByTestId('admin-users-view')).toBeVisible()
  await expect(page.getByTestId('admin-view')).not.toContainText(/Отзывов|Баллов/)
  await expectContained(page.getByTestId('admin-view'))
  await page.screenshot({ path: testInfo.outputPath('admin-users.png'), fullPage: true })
  await page.getByTestId('admin-tab-stats').click()
  await expect(page.getByTestId('admin-stats-grid')).toBeVisible()
  await expect(page.getByTestId('admin-stat-scored-month')).toHaveCount(0)
  await expect(page.getByTestId('admin-stat-readings-week')).toContainText('143')
  await expectContained(page.getByTestId('admin-view'))
  await page.screenshot({ path: testInfo.outputPath('admin-stats.png'), fullPage: true })
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


test('audience filters combine with search, reset pagination, and support keyboard switching', async ({ page }, testInfo) => {
  const requests = await openAdmin(page, '/admin', adminAudienceReadingFixtures, true)
  await showAllNewest(page)
  const rows = page.getByTestId('admin-reading-row')
  await expect(rows).toHaveCount(20)
  await expect(page.getByTestId('admin-reading-audience')).toHaveValue('all')
  await page.screenshot({ path: testInfo.outputPath('admin-audience-all.png') })
  await page.getByTestId('admin-reading-next').click()
  await expect(rows.first()).toContainText('Архивный вопрос 21')
  await page.getByTestId('admin-reading-search').fill('Архивный')
  await page.getByTestId('admin-reading-search').press('Enter')
  await expect(rows.first()).toContainText('Архивный вопрос 3')
  const matches = adminAudienceReadingFixtures.filter(row => row.question.includes('Архивный'))

  await page.getByTestId('admin-reading-audience').selectOption('registered')
  await expect(page.getByTestId('admin-reading-audience')).toHaveValue('registered')
  await expect(rows).toHaveCount(20)
  const registered = matches.filter(row => row.userId !== null)
  await expect(page.getByTestId('admin-readings-total')).toHaveText(`Всего: ${registered.length}`)
  await expect(page.getByTestId('admin-reading-guest')).toHaveCount(0)
  // Registered includes active subscribers and users whose access has expired.
  await expect(page.getByTestId('admin-subscription-purchase').first()).toBeVisible()
  await expect(rows.filter({ hasText: 'Архивный вопрос 8:' })).toHaveCount(1)
  await expect(page.getByTestId('admin-reading-search')).toHaveValue('Архивный')
  expect(requests.readings.at(-1)).toEqual({ page: 1, search: 'Архивный', audience: 'registered' })
  await page.getByTestId('admin-reading-next').click()
  await expect(rows).toHaveCount(registered.length - 20)

  await page.getByTestId('admin-reading-audience').selectOption('subscribers')
  const subscribers = matches.filter(row => row.subscription?.isActive)
  await expect(rows).toHaveCount(subscribers.length)
  await expect(page.getByTestId('admin-readings-total')).toHaveText(`Всего: ${subscribers.length}`)
  expect(requests.readings.at(-1)).toEqual({ page: 1, search: 'Архивный', audience: 'subscribers' })
  await expect(page.getByTestId('admin-reading-prev')).toBeDisabled()
  await expect(rows.filter({ hasText: 'Архивный вопрос 4:' })).toHaveCount(1) // manual active access
  await expect(rows.filter({ hasText: 'Архивный вопрос 8:' })).toHaveCount(0) // expired paid access
  await page.screenshot({ path: testInfo.outputPath('admin-audience-subscribers.png') })

  const audience = page.getByTestId('admin-reading-audience')
  await audience.selectOption('guests')
  await expect(audience).toHaveValue('guests')
  await expect(rows).toHaveCount(0)
  await expect(page.getByRole('status').filter({ hasText: 'Загружаю' })).toBeVisible()
  requests.releaseGuests()
  await expect(rows).toHaveCount(matches.filter(row => row.userId === null).length)
  await expect(page.getByTestId('admin-reading-user')).toHaveCount(0)
  const requestCount = requests.readings.length
  await audience.selectOption('guests')
  expect(requests.readings).toHaveLength(requestCount)
  await page.screenshot({ path: testInfo.outputPath('admin-audience-guests.png') })
  await expectContained(page.getByTestId('admin-view'))
  await page.getByTestId('admin-reading-unseen').focus()
  await page.getByTestId('admin-reading-unseen').press('Enter')
  await expect(page.getByTestId('admin-reading-unseen')).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByTestId('admin-readings-empty')).toBeVisible()
  expect(requests.unexpected).toEqual([])
})

for (const width of [320, 390]) {
  test(`message filters fit at ${width}px with readable labels and touch targets`, async ({ page }, testInfo) => {
    await page.setViewportSize({ width, height: 844 })
    await openAdmin(page, '/admin', adminAudienceReadingFixtures)
    const filters = page.getByTestId('admin-reading-filters')
    await expect(page.getByTestId('admin-reading-unseen')).toHaveAttribute('aria-pressed', 'true')
    await expect(page.getByTestId('admin-reading-sort')).toHaveValue('oldest')
    await expect(page.getByTestId('admin-reading-audience')).toHaveValue('all')
    await expect(page.getByRole('combobox', { name: 'Авторы', exact: true })).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Порядок', exact: true })).toBeVisible()
    for (const control of await filters.locator('button, select').all()) {
      await expect(control).toBeVisible()
      expect((await control.boundingBox())!.height).toBeGreaterThanOrEqual(44)
    }
    await expectContained(page.getByTestId('admin-view'))
    await filters.screenshot({ path: testInfo.outputPath(`admin-filters-${width}.png`) })
    await page.screenshot({ path: testInfo.outputPath(`admin-unread-${width}.png`) })
    await page.getByTestId('admin-reading-seen-all').click()
    await page.getByTestId('admin-reading-audience').selectOption('registered')
    await expect(page.getByTestId('admin-readings-total')).toHaveText('Всего: 31')
    await expectContained(page.getByTestId('admin-view'))
  })
}

for (const path of ['/feedback', '/feedback/legacy-token']) {
  test(`retired public link ${path} opens clean home without feedback requests`, async ({ page, context }) => {
    const requests = await installPaidAccessFixture(context, { authenticated: false })
    await page.goto(`${path}?token=legacy-secret#legacy-secret`)
    await expect(page).toHaveURL(/^http:\/\/127\.0\.0\.1:4173\/$/)
    await expect(page.getByRole('heading', { level: 1, name: 'Открой свой расклад' })).toBeVisible()
    expect(requests.unexpectedApis).toEqual([])
    expect(requests.checkoutAttempts).toEqual([])
  })
}

test('viewed batches persist across navigation and unread oldest-first queue never skips messages', async ({ page }, testInfo) => {
  const requests = await openAdmin(page)
  await expect.poll(() => requests.viewed.size).toBe(20)
  expect(requests.viewedBatches[0]).toEqual([...adminReadingFixtures].reverse().slice(0, 20).map(row => row.id))
  await expect(page.getByTestId('admin-reading-unseen')).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByTestId('admin-reading-sort')).toHaveValue('oldest')
  await expect(page.getByTestId('admin-reading-row').first()).toContainText('Архивный вопрос 41')
  expect(requests.answers).toEqual([])
  await page.getByTestId('admin-reading-next-unseen').click()
  await expect.poll(() => requests.viewed.size).toBe(40)
  await expect(page.getByTestId('admin-reading-row')).toHaveCount(20)
  await page.getByTestId('admin-reading-next-unseen').click()
  await expect(page.getByTestId('admin-reading-row')).toHaveCount(1)
  await expect.poll(() => requests.viewed.size).toBe(41)
  expect(new Set(requests.viewedBatches.flat()).size).toBe(41)
  await expect(page.getByTestId('admin-reading-next-unseen')).toBeDisabled()
  await page.getByTestId('admin-readings-refresh').click()
  await expect(page.getByTestId('admin-readings-empty')).toContainText('Непрочитанных')
  await page.getByTestId('admin-reading-seen-all').click()
  await page.getByTestId('admin-reading-sort').selectOption('oldest')
  await expect(page.getByTestId('admin-reading-row').first()).toContainText('Архивный вопрос 41')
  await expect(page.getByTestId('admin-reading-row')).toHaveCount(20)
  await expectContained(page.getByTestId('admin-view'))
  await page.screenshot({ path: testInfo.outputPath('admin-unseen-oldest.png') })
  await page.getByTestId('admin-tab-users').click()
  await page.getByTestId('admin-tab-readings').click()
  await page.getByTestId('admin-reading-unseen').click()
  await expect(page.getByTestId('admin-readings-empty')).toBeVisible()
  expect(requests.unexpected).toEqual([])
})
