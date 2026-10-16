import { expect, test, type Page } from '@playwright/test'
import { fullReading } from './guestReading.fixture'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

async function openHistory(page: Page, count = 1) {
  const reading = { ...fullReading, question: `Как лучше подготовиться к переменам? ${'длинныйвопрос'.repeat(12)}` }
  const readings = Array.from({ length: count }, (_, index) => ({
    ...reading, id: index === 0 ? reading.id : `history-${index}`, question: `${reading.question}${index === 0 ? '' : ` №${index}`}`,
  }))
  const state = { deleted: false, attempts: 0, unexpectedRequests: [] as string[], historyPages: [] as number[] }
  const responses: Record<string, unknown> = {
    '/api/public/config': { supportEmail: '', paymentsEnabled: false },
    '/api/public/legal-documents': legalDocumentsResponse,
    '/api/announcements/unread': [],
    '/api/subscription/status': {
      isActive: false, canCreateIntroReading: false, canCreateFreeReading: false, freeReadingsUsedToday: 1, freeReadingsDailyLimit: 1,
    },
  }
  await page.route('**/api/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (!path.startsWith('/api/')) return route.fallback()
    if (path.startsWith('/api/readings/')) {
      expect(request.headers().authorization).toBe('Bearer local-history-fixture')
    }
    if (request.method() === 'GET' && path === '/api/readings/history/summary') {
      const pageNumber = Number(new URL(request.url()).searchParams.get('page'))
      const pageSize = Number(new URL(request.url()).searchParams.get('pageSize'))
      expect(pageSize).toBe(20)
      state.historyPages.push(pageNumber)
      const retained = state.deleted ? readings.filter(item => item.id !== reading.id) : readings
      return route.fulfill({ json: {
        items: retained.slice((pageNumber - 1) * pageSize, pageNumber * pageSize).map(item => {
          const { cards: _cards, interpretation, ...summary } = item
          return { ...summary, interpretationPreview: interpretation?.slice(0, 240) ?? null }
        }),
        total: retained.length, page: pageNumber, pageSize,
      } })
    }
    if (request.method() === 'DELETE' && path === `/api/readings/${reading.id}`) {
      state.attempts++
      if (state.attempts === 1) {
        return route.fulfill({ status: 503, json: { message: 'Не удалось удалить расклад. Повторите попытку.' } })
      }
      state.deleted = true
      return route.fulfill({ status: 204 })
    }
    if (request.method() === 'GET' && path in responses) {
      return route.fulfill({ json: responses[path] })
    }
    state.unexpectedRequests.push(`${request.method()} ${path}`)
    return route.fulfill({ status: 404, json: { message: 'Unexpected test request' } })
  })
  await page.addInitScript(() => {
    localStorage.setItem('fv_token', 'local-history-fixture')
    localStorage.setItem('fv_email', 'history@example.com')
    localStorage.setItem('fv_user_id', 'local-history-user')
  })
  await page.goto('/history')
  await page.getByTestId('accept-necessary').click()
  await expect(page.locator('.history-item').first()).toContainText(reading.question)
  await page.evaluate(() => document.fonts.ready)
  return state
}

test('mobile history confirms soft deletion, keeps failed attempts and hides the row after reload', async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 393, height: 851 })
  await page.emulateMedia({ reducedMotion: 'reduce' })
  const state = await openHistory(page)
  await page.getByTestId('delete-reading').click()
  await expect(page.getByTestId('delete-reading-form')).toBeVisible()
  await expect(page.getByTestId('confirm-delete-reading')).toBeFocused()
  await expect(page.getByTestId('delete-reading-form').locator('input')).toHaveCount(0)

  // App overflow clipping can conceal layout bugs from document.scrollWidth alone.
  const overflowing = await page.locator('.history-page, .history-page *').evaluateAll(elements =>
    elements.filter(element => {
      const bounds = element.getBoundingClientRect()
      return bounds.width > 0 && bounds.height > 0
        && (bounds.left < -1 || bounds.right > window.innerWidth + 1)
    }).map(element => element.getAttribute('data-testid') ?? element.className),
  )
  expect(overflowing).toEqual([])
  await page.screenshot({ path: testInfo.outputPath('history-confirmation-mobile.png'), fullPage: true })

  await page.getByTestId('cancel-delete-reading').click()
  await expect(page.getByTestId('delete-reading-form')).toHaveCount(0)
  await expect(page.getByTestId('delete-reading')).toBeFocused()
  expect(state.attempts).toBe(0)

  await page.getByTestId('delete-reading').click()
  await page.getByTestId('confirm-delete-reading').click()
  await expect(page.getByRole('alert')).toContainText('Повторите попытку')
  await expect(page.locator('.history-item')).toHaveCount(1)
  await expect(page.getByTestId('confirm-delete-reading')).toBeEnabled()
  expect(state.deleted).toBe(false)

  await page.getByTestId('confirm-delete-reading').click()
  await expect(page.locator('.history-item')).toHaveCount(0)
  await expect(page.getByTestId('delete-reading-form')).toHaveCount(0)
  await expect(page.getByText('Пока что пусто. Сделай первый расклад.')).toBeVisible()
  await page.reload()
  await expect(page.getByText('Пока что пусто. Сделай первый расклад.')).toBeVisible()
  expect(state.attempts).toBe(2)
  expect(state.unexpectedRequests).toEqual([])
})

test('history pages beyond fifty records and scrolls back to its heading at narrow widths', async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 320, height: 780 })
  await page.emulateMedia({ reducedMotion: 'reduce' })
  const state = await openHistory(page, 61)
  await expect(page.locator('.history-item')).toHaveCount(20)
  for (let next = 2; next <= 4; next++) {
    await page.getByTestId('history-next').click()
    await expect(page.getByText(`Страница ${next} из 4`)).toBeVisible()
    await expect(page.locator('.history-heading')).toBeFocused()
    expect((await page.locator('.history-heading').boundingBox())!.y).toBeLessThan(160)
  }
  await expect(page.locator('.history-item')).toHaveCount(1)
  await expect(page.locator('.history-item')).toContainText('№60')
  await expect(page.getByTestId('history-next')).toBeDisabled()
  const overflow = await page.locator('.history-page, .history-page *').evaluateAll(elements =>
    elements.some(element => {
      const bounds = element.getBoundingClientRect()
      return bounds.width > 0 && (bounds.left < -1 || bounds.right > window.innerWidth + 1)
    }))
  expect(overflow).toBe(false)
  await page.screenshot({ path: testInfo.outputPath('history-summary-pagination-mobile.png'), fullPage: true })
  expect(state.historyPages).toEqual([1, 2, 3, 4])
  expect(state.unexpectedRequests).toEqual([])
})
