import { expect, test, type Page } from '@playwright/test'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

const reading = {
  id: '11111111-1111-4111-8111-111111111111', spreadType: 3, spreadName: 'Три карты',
  question: 'Как выбрать следующий шаг?', createdAt: '2026-10-16T12:00:00Z', deckType: 1,
  cards: [], isPreview: false, interpretation: 'Готовое толкование. '.repeat(800) + 'ФИНАЛ ГОТОВОГО ОТВЕТА',
}
async function fixture(page: Page, restore = false) {
  const calls = { subscription: 0, unexpected: [] as string[] }
  await page.addInitScript(({ id, restore }) => {
    localStorage.clear()
    localStorage.setItem('fv_token', 'result-test-token')
    localStorage.setItem('fv_user_id', 'result-test-user')
    localStorage.setItem('fv_email', 'result@example.com')
    if (restore) localStorage.setItem('fv_guest_reading_unlocked_v1', JSON.stringify({
      readingId: id, ownerUserId: 'result-test-user', expiresAt: new Date(Date.now() + 86400000).toISOString(),
    }))
  }, { id: reading.id, restore })
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname
    if (!path.startsWith('/api/')) return route.fallback()
    if (route.request().method() !== 'GET') {
      calls.unexpected.push(path)
      return route.fulfill({ status: 405, json: {} })
    }
    if (path === '/api/subscription/status') {
      calls.subscription++
      await new Promise(resolve => setTimeout(resolve, 150))
      return route.fulfill({ json: { status: 0, expiresAt: null, isActive: calls.subscription > 1,
        freeReadingsUsedToday: 0, freeReadingsDailyLimit: 1, canCreateIntroReading: true, canCreateFreeReading: true } })
    }
    const responses: Record<string, unknown> = {
      '/api/public/config': { supportEmail: '', paymentsEnabled: false },
      '/api/public/legal-documents': legalDocumentsResponse,
      '/api/announcements/unread': [],
      [`/api/readings/${reading.id}`]: reading,
    }
    if (path in responses) return route.fulfill({ json: responses[path] })
    calls.unexpected.push(path)
    return route.fulfill({ status: 404, json: {} })
  })
  await page.emulateMedia({ reducedMotion: 'reduce' })
  return calls
}
async function acceptNecessary(page: Page) {
  const necessary = page.getByTestId('accept-necessary')
  if (await necessary.isVisible()) await necessary.click()
}

test('restored complete results render the final paragraph immediately', async ({ page }, testInfo) => {
  const calls = await fixture(page, true)
  await page.goto('/result')
  await expect(page.locator('.prose-mystic')).toContainText('ФИНАЛ ГОТОВОГО ОТВЕТА', { timeout: 1500 })
  await expect(page.locator('.caret')).toHaveCount(0)
  await acceptNecessary(page)
  await page.screenshot({ path: testInfo.outputPath('completed-result-immediate.png') })
  expect(calls.unexpected).toEqual([])
})

test('live text is progressive, final text flushes immediately, and user scrolling is respected', async ({ page }, testInfo) => {
  const calls = await fixture(page)
  await page.goto('/')
  await acceptNecessary(page)
  // Drive the same public store state delivered by SSE; this does not invoke generation APIs.
  await page.evaluate(async value => {
    const { useReadingStore } = await import(new URL('/src/stores/useReadingStore.ts', location.href).href)
    const { router } = await import(new URL('/src/router/index.ts', location.href).href)
    const store = useReadingStore()
    store.current = { ...value, interpretation: null }
    store.cardsReady = true
    store.loading = true
    store.streamingDone = false
    await router.push('/result')
  }, reading)
  await expect(page.locator('.caret')).toBeVisible()
  expect(await page.locator('.caret').evaluate(element => getComputedStyle(element).animationName)).toBe('none')
  await page.evaluate(async () => {
    const { useReadingStore } = await import(new URL('/src/stores/useReadingStore.ts', location.href).href)
    useReadingStore().streamingText = '## Первый фрагмент\n\n' + 'Размышление о следующем шаге.\n\n'.repeat(80)
  })
  await expect(page.locator('.prose-mystic')).toContainText('Первый фрагмент', { timeout: 500 })
  await expect.poll(() => page.evaluate(() => scrollY)).toBeGreaterThan(500)
  await page.mouse.wheel(0, -10000)
  await expect.poll(() => page.evaluate(() => scrollY)).toBeLessThan(100)
  await page.evaluate(async () => {
    const { useReadingStore } = await import(new URL('/src/stores/useReadingStore.ts', location.href).href)
    useReadingStore().streamingText += 'НОВЫЙ ФРАГМЕНТ\n\n' + 'Ещё один абзац.\n\n'.repeat(30)
  })
  await expect(page.locator('.prose-mystic')).toContainText('НОВЫЙ ФРАГМЕНТ', { timeout: 500 })
  expect(await page.evaluate(() => scrollY)).toBeLessThan(100)
  await page.evaluate(async () => {
    const { useReadingStore } = await import(new URL('/src/stores/useReadingStore.ts', location.href).href)
    const store = useReadingStore()
    store.streamingText += 'ФИНАЛ ПОТОКА'
    store.streamingDone = true
    store.loading = false
  })
  await expect(page.locator('.prose-mystic')).toContainText('ФИНАЛ ПОТОКА', { timeout: 300 })
  await expect(page.locator('.caret')).toHaveCount(0)
  expect(await page.evaluate(() => scrollY)).toBeLessThan(100)
  await page.screenshot({ path: testInfo.outputPath('stream-final-user-position.png') })
  expect(calls.unexpected).toEqual([])
})

test('header and home share status loading, then payment explicitly refreshes it', async ({ page }) => {
  const calls = await fixture(page)
  await page.goto('/')
  await acceptNecessary(page)
  await expect(page.getByTestId('header-quota')).toHaveText('1/1')
  await expect(page.getByRole('button', { name: 'Начать расклад', exact: true })).toBeDisabled()
  expect(calls.subscription).toBe(1)
  await page.evaluate(async () => {
    const { router } = await import(new URL('/src/router/index.ts', location.href).href)
    await router.push('/payment/success')
  })
  await expect(page.getByRole('heading', { name: 'Доступ активен', exact: true })).toBeVisible()
  expect(calls.subscription).toBe(2)
  expect(calls.unexpected).toEqual([])
})
