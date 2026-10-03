/// <reference types="node" />
import { execFileSync } from 'node:child_process'
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import seo from '../../src/seo/routes.json'

const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')
let outputDir: string

async function artifact(relativePath: string) {
  return readFile(path.join(outputDir, relativePath), 'utf8')
}

describe('generated SEO artifacts', () => {
  beforeAll(async () => {
    outputDir = await mkdtemp(path.join(tmpdir(), 'future-viewer-seo-'))
    const sourceHtml = await readFile(path.join(frontendRoot, 'index.html'), 'utf8')
    await writeFile(path.join(outputDir, 'index.html'), sourceHtml)
    execFileSync(process.execPath, [path.join(frontendRoot, 'scripts/generate-seo.mjs'), '--out-dir', outputDir], {
      env: { ...process.env, VITE_SITE_URL: 'https://alex-taro.ru' },
      stdio: 'pipe',
    })
  })

  afterAll(async () => {
    if (outputDir) await rm(outputDir, { recursive: true, force: true })
  })

  it('ships an honest guest landing page even before JavaScript renders', async () => {
    const html = await artifact('index.html')

    expect(html).toContain(`<title>${seo.defaultTitle}</title>`)
    expect(html).toContain(`<meta name="description" content="${seo.defaultDescription}" />`)
    expect(html).toContain('<h1>Одна карта Таро бесплатно — без регистрации</h1>')
    expect(html).toContain('первую половину толкования')
    expect(html).toContain('Зарегистрируйтесь и подтвердите email')
    expect(html).toContain('<link rel="canonical" href="https://alex-taro.ru/" />')
    expect(html.match(/<title>/g)).toHaveLength(1)
    expect(html.match(/name="description"/g)).toHaveLength(1)

    const json = html.match(/<script id="seo-managed-jsonld" type="application\/ld\+json">(.*?)<\/script>/)?.[1]
    const structured = JSON.parse(json!)
    expect(structured.map((entry: { '@type': string }) => entry['@type'])).toEqual(['WebSite', 'WebPage'])
    expect(json).not.toMatch(/AggregateRating|Offer|SearchAction|BreadcrumbList/)
  })

  it('keeps account, results and token pages out of sitemap and public structured data', async () => {
    const sitemap = await artifact('sitemap.xml')
    expect(sitemap).toContain('<loc>https://alex-taro.ru/</loc>')
    expect(sitemap).toContain('<loc>https://alex-taro.ru/tarot/cards/shut</loc>')
    expect(sitemap).not.toMatch(/<loc>[^<]*(?:\/auth|\/result|\/reading|\/profile|\/admin|\/feedback|\/verify-email)/)
    expect(sitemap).not.toMatch(/utm_|yclid|<lastmod>/)

    for (const page of ['auth/index.html', 'result/index.html', 'verify-email/index.html', 'admin/users/index.html', 'noindex.html']) {
      const html = await artifact(page)
      expect(html).toContain('<meta name="robots" content="noindex, nofollow" />')
      expect(html).not.toContain('application/ld+json')
      expect(html).not.toContain('seo-static-fallback')
    }
  })

  it('publishes the sitemap without blocking crawlers from seeing private-page noindex', async () => {
    const robots = await artifact('robots.txt')
    expect(robots).toContain('Sitemap: https://alex-taro.ru/sitemap.xml')
    expect(robots).toContain('Disallow: /api/')
    expect(robots).not.toMatch(/Disallow: \/(?:auth|result|profile|admin)/)
  })
})
