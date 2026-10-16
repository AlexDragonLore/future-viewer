import { expect, test, type Page } from '@playwright/test'
import { adminFixtureResponses, adminFixtureSession } from '../fixtures/admin'

async function seoState(page: Page) {
  return page.evaluate(() => ({
    canonical: document.querySelector<HTMLLinkElement>('link[rel="canonical"]')?.href,
    robots: document.querySelector<HTMLMetaElement>('meta[name="robots"]')?.content,
    jsonld: [...document.querySelectorAll('script[type="application/ld+json"]')].flatMap(script => JSON.parse(script.textContent || '[]')),
    sameDocument: document.documentElement.dataset.seoQaDocument === 'client-navigation',
  }))
}
async function headerLink(page: Page, desktop: string, mobile: string) {
  if (await page.getByTestId(desktop).isVisible()) return page.getByTestId(desktop).click()
  await page.getByTestId('burger-button').click()
  await page.getByTestId(mobile).click()
}

test('catalog SEO loads only for content and clears on client navigation to private routes', async ({ page }, testInfo) => {
  const catalogRequests: string[] = []
  const unexpected: string[] = []
  const pageErrors: string[] = []
  page.on('pageerror', error => pageErrors.push(error.message))
  page.on('request', request => {
    if (/\/tarotSeoCatalog(?:-[^/]+)?\.js$/.test(new URL(request.url()).pathname)) catalogRequests.push(request.url())
  })
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url())
    if (!url.pathname.startsWith('/api/')) return route.fallback()
    if (url.pathname === '/api/auth/login' && request.method() === 'POST') {
      return route.fulfill({ json: {
        accessToken: adminFixtureSession.token, userId: adminFixtureSession.userId,
        email: adminFixtureSession.email, isAdmin: true, expiresAt: '2030-01-01T00:00:00Z',
      } })
    }
    if (request.method() === 'GET' && url.pathname in adminFixtureResponses) {
      return route.fulfill({ json: adminFixtureResponses[url.pathname] })
    }
    unexpected.push(`${request.method()} ${url.pathname}`)
    return route.fulfill({ status: 404, json: {} })
  })
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.goto('/')
  await expect(page.getByRole('heading', { level: 1, name: 'Открой свой расклад' })).toBeVisible()
  await page.getByTestId('accept-necessary').click()
  await page.evaluate(() => { document.documentElement.dataset.seoQaDocument = 'client-navigation' })
  expect(catalogRequests).toEqual([])
  const home = await seoState(page)
  expect(home.canonical).toBe('https://alex-taro.ru/')
  expect(home.robots).toContain('index, follow')
  expect(home.jsonld.map(item => item['@type'])).toContain('WebSite')

  await headerLink(page, 'nav-auth', 'burger-auth')
  await expect(page.getByRole('heading', { level: 1, name: 'Войти' })).toBeVisible()
  const auth = await seoState(page)
  expect(auth.canonical).toBe('https://alex-taro.ru/auth')
  expect(auth.robots).toBe('noindex, nofollow')
  expect(auth.jsonld).toEqual([])
  expect(auth.sameDocument).toBe(true)
  expect(catalogRequests).toEqual([])
  await page.screenshot({ path: testInfo.outputPath('auth-no-catalog.png') })

  await page.getByRole('textbox', { name: 'Электронная почта' }).fill(adminFixtureSession.email)
  await page.getByLabel('Пароль', { exact: true }).fill('local-test-password')
  await page.getByRole('button', { name: 'Войти', exact: true }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Загляни за Вуаль' })).toBeVisible()
  expect(catalogRequests).toEqual([])
  await page.getByRole('link', { name: 'Вопросы и ответы', exact: true }).last().click()
  await expect(page.getByRole('heading', { level: 1, name: 'Вопросы и ответы об онлайн-раскладах' })).toBeVisible()
  await expect.poll(async () => (await seoState(page)).jsonld.some(item => item['@type'] === 'FAQPage')).toBe(true)
  const faq = await seoState(page)
  expect(faq.canonical).toBe('https://alex-taro.ru/faq')
  expect(faq.robots).toContain('index, follow')
  expect(faq.sameDocument).toBe(true)
  const faqSchema = faq.jsonld.find(item => item['@type'] === 'FAQPage')
  expect(faqSchema.mainEntity).toHaveLength(await page.locator('.faq-item').count())
  expect(faqSchema.mainEntity[0]['@type']).toBe('Question')
  expect(faqSchema.mainEntity[0].name).toBe(await page.locator('.faq-item h2').first().textContent())
  expect(faqSchema.mainEntity[0].acceptedAnswer.text).toBe(await page.locator('.faq-item p').first().textContent())
  expect(catalogRequests).toHaveLength(1)
  await page.screenshot({ path: testInfo.outputPath('faq-content-seo.png') })

  await headerLink(page, 'nav-admin', 'burger-admin')
  await expect(page).toHaveURL(/\/admin\/readings$/)
  await expect(page.getByTestId('admin-readings-view')).toBeVisible()
  const admin = await seoState(page)
  expect(admin.canonical).toBe('https://alex-taro.ru/admin/readings')
  expect(admin.robots).toBe('noindex, nofollow')
  expect(admin.jsonld).toEqual([])
  expect(admin.sameDocument).toBe(true)
  expect(catalogRequests).toHaveLength(1)
  expect(unexpected).toEqual([])
  expect(pageErrors).toEqual([])
  await page.screenshot({ path: testInfo.outputPath('private-admin-cleared-seo.png') })
  await testInfo.attach('seo-navigation-proof', { body: JSON.stringify({ home, auth, faq, admin, catalogRequests, unexpected, pageErrors }, null, 2), contentType: 'application/json' })
})
