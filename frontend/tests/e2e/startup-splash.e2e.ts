import { expect, test, type Page } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

const productionCsp = readFileSync(new URL('../../../deploy/Caddyfile', import.meta.url), 'utf8')
  .match(/Content-Security-Policy "([^"]+)"/)?.[1]
if (!productionCsp) throw new Error('Production Content-Security-Policy is missing')
// Keep production script restrictions; only allow the local Vite HMR connection.
const developmentCsp = (origin: string) => productionCsp.replace(
  "connect-src 'self'",
  `connect-src 'self' ${origin.replace(/^http/, 'ws')}`,
)

async function mockRequests(page: Page) {
  await page.route('**/*', async route => {
    const url = new URL(route.request().url())
    if (!['127.0.0.1', 'localhost'].includes(url.hostname)) return route.abort()
    if (route.request().resourceType() === 'document') {
      const response = await route.fetch()
      return route.fulfill({
        response,
        headers: { ...response.headers(), 'content-security-policy': developmentCsp(url.origin) },
      })
    }
    if (!url.pathname.startsWith('/api/')) return route.continue()

    const responses: Record<string, unknown> = {
      '/api/public/config': { supportEmail: '', paymentsEnabled: false },
      '/api/public/legal-documents': legalDocumentsResponse,
      '/api/announcements/unread': [],
    }
    if (route.request().method() !== 'GET' || !(url.pathname in responses)) {
      return route.fulfill({ status: 404, json: { message: 'Unexpected startup test request' } })
    }
    return route.fulfill({ json: responses[url.pathname] })
  })
}

async function holdApplicationModule(page: Page) {
  let release!: () => void
  const gate = new Promise<void>(resolve => { release = resolve })
  await page.route(/\/src\/main\.ts(?:\?.*)?$/, async route => {
    await gate
    await route.continue()
  })
  return release
}

test.beforeEach(async ({ page }) => {
  await mockRequests(page)
})

test('shows the deck before application JS and reveals the first route without repeating on navigation', async ({ page }, testInfo) => {
  const release = await holdApplicationModule(page)
  try {
    await page.goto('/', { waitUntil: 'commit' })
    const splash = page.locator('#startup-splash')
    await expect(splash).toBeVisible()
    await expect(page.locator('.startup-card')).toHaveCount(7)
    await expect(page.locator('#startup-status')).toHaveText('Тасуем колоду…')
    await expect(page.locator('#startup-retry')).toBeHidden()
    await expect(page.locator('body')).toHaveClass(/startup-pending/)
    await expect(page.locator('#app')).toHaveAttribute('inert', '')
    await expect(page.locator('#app')).toHaveAttribute('aria-hidden', 'true')
    expect(await splash.evaluate(element => element.closest('#app'))).toBeNull()
    expect(await page.locator('.startup-card').first().evaluate(element => getComputedStyle(element).animationName)).not.toBe('none')

    const viewport = page.viewportSize()!
    await expect(splash).toHaveJSProperty('clientWidth', viewport.width)
    await expect(splash).toHaveJSProperty('clientHeight', viewport.height)
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(viewport.width)
    const screenshot = testInfo.outputPath('startup-shuffling-deck.png')
    await page.screenshot({ path: screenshot })
    await testInfo.attach('Initial shuffling deck', { path: screenshot, contentType: 'image/png' })

    release()
    await expect(page.locator('.home-page')).toBeVisible()
    await expect(splash).toHaveCount(0)
    await expect(page.locator('body')).not.toHaveClass(/startup-pending/)
    await expect(page.locator('#app')).not.toHaveAttribute('inert')
    await expect(page.locator('#app')).not.toHaveAttribute('aria-hidden')

    await page.getByTestId('accept-necessary').click()
    await page.getByRole('navigation', { name: 'О сервисе' }).getByRole('link', { name: 'Вопросы и ответы', exact: true }).click()
    await expect(page).toHaveURL(/\/faq$/)
    await expect(page.locator('main')).toBeVisible()
    await expect(splash).toHaveCount(0)
    await expect(page.locator('body')).not.toHaveClass(/startup-pending/)
  } finally {
    release()
  }
})

test('reduced motion keeps the deck still and skips the minimum presentation delay', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.clock.install()
  await page.clock.pauseAt(new Date())
  const release = await holdApplicationModule(page)
  try {
    await page.goto('/', { waitUntil: 'commit' })
    await expect(page.locator('#startup-splash')).toBeVisible()
    const animationNames = await page.locator('.startup-card').evaluateAll(cards => cards.map(card => getComputedStyle(card).animationName))
    expect(animationNames).toEqual(Array(7).fill('none'))

    // Exercise the standalone controller while the application bundle is still unavailable.
    await page.evaluate(() => document.dispatchEvent(new Event('fv:app-ready')))
    await page.clock.runFor(600)
    await expect(page.locator('#startup-splash')).toHaveCount(0)
    await expect(page.locator('#app')).not.toHaveAttribute('inert')
  } finally {
    release()
  }
})

test('a failed initial script exposes a working reload action', async ({ page }) => {
  await page.clock.install()
  let attempts = 0
  await page.route(/\/src\/main\.ts(?:\?.*)?$/, route => {
    attempts += 1
    return attempts === 1 ? route.abort('failed') : route.continue()
  })
  await page.goto('/')
  const retry = page.locator('#startup-retry')
  await expect(retry).toBeVisible()
  await expect(page.locator('#startup-status')).toHaveText('Не удалось открыть сайт')
  await expect(page.locator('#startup-splash')).toHaveClass(/startup-failed/)
  await page.clock.fastForward(12_100)
  await expect(page.locator('#startup-status')).toHaveText('Не удалось открыть сайт')
  await expect(page.locator('#startup-splash')).toHaveClass(/startup-failed/)
  expect(await page.locator('.startup-card').first().evaluate(element => getComputedStyle(element).animationPlayState)).toBe('paused')
  await retry.click()
  await expect(page.locator('.home-page')).toBeVisible()
  await expect(page.locator('#startup-splash')).toHaveCount(0)
  expect(attempts).toBe(2)
})

test('a stalled download offers retry and still recovers when the application arrives', async ({ page }) => {
  await page.clock.install()
  const release = await holdApplicationModule(page)
  try {
    await page.goto('/', { waitUntil: 'commit' })
    await expect(page.locator('#startup-splash')).toBeVisible()
    await expect(page.locator('#startup-retry')).toBeHidden()
    await page.clock.fastForward(12_100)
    await expect(page.locator('#startup-retry')).toBeVisible()

    release()
    await expect(page.locator('.home-page')).toBeVisible()
    await expect(page.locator('#startup-splash')).toHaveCount(0)
    await expect(page.locator('#app')).not.toHaveAttribute('inert')
  } finally {
    release()
  }
})

test('a failed startup controller cannot hide the mounted application', async ({ page }) => {
  let controllerRequests = 0
  await page.route(/\/startup\.js(?:\?.*)?$/, route => {
    controllerRequests += 1
    return route.abort('failed')
  })
  await page.goto('/')
  await expect(page.locator('.home-page')).toBeVisible()
  await expect(page.locator('#startup-splash')).toHaveCount(0)
  await expect(page.locator('body')).not.toHaveClass(/startup-pending/)
  await expect(page.locator('#app')).not.toHaveAttribute('inert')
  await expect(page.locator('#app')).not.toHaveAttribute('aria-hidden')
  expect(controllerRequests).toBe(1)
})
