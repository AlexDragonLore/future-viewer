import { mkdir, readFile, writeFile } from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const dist = path.join(root, 'dist')
const seoPath = path.join(root, 'src/seo/routes.json')

const seo = JSON.parse(await readFile(seoPath, 'utf8'))
const baseHtml = await readFile(path.join(dist, 'index.html'), 'utf8')
const today = new Date().toISOString().slice(0, 10)
const iconAssetVersion = '20260516'

function cleanSiteUrl(value) {
  const raw = value && value.trim() ? value.trim() : seo.siteUrl
  return raw.replace(/\/+$/, '')
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('"', '&quot;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
}

function absoluteUrl(siteUrl, value) {
  if (/^https?:\/\//i.test(value)) return value
  return `${siteUrl}${value.startsWith('/') ? value : `/${value}`}`
}

function routeOutputPath(routePath) {
  if (routePath === '/') return path.join(dist, 'index.html')
  return path.join(dist, routePath.replace(/^\/+/, ''), 'index.html')
}

function managedHead(route, { index }) {
  const siteUrl = cleanSiteUrl(process.env.VITE_SITE_URL)
  const title = route?.title ?? seo.defaultTitle
  const description = route?.description ?? seo.defaultDescription
  const canonical = absoluteUrl(siteUrl, index ? route.path : route.path.replace(/:[^/]+/g, ''))
  const image = absoluteUrl(siteUrl, seo.defaultImage)
  const robots = index
    ? 'index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1'
    : 'noindex, nofollow'
  const googleVerification = process.env.VITE_GOOGLE_SITE_VERIFICATION?.trim()
  const yandexVerification = process.env.VITE_YANDEX_VERIFICATION?.trim()
  const jsonLd = index
    ? JSON.stringify(
        [
          {
            '@context': 'https://schema.org',
            '@type': 'WebSite',
            name: seo.siteName,
            url: siteUrl,
            inLanguage: 'ru-RU',
          },
          {
            '@context': 'https://schema.org',
            '@type': 'Organization',
            name: seo.siteName,
            url: siteUrl,
            logo: absoluteUrl(siteUrl, '/icons/icon-512.png'),
            contactPoint: {
              '@type': 'ContactPoint',
              contactType: 'customer support',
              availableLanguage: 'Russian',
            },
          },
        ],
        null,
        0,
      )
    : null

  const lines = [
    '<!-- seo:managed:start -->',
    `    <title>${escapeHtml(title)}</title>`,
    `    <meta name="description" content="${escapeHtml(description)}" />`,
    `    <meta name="robots" content="${escapeHtml(robots)}" />`,
    `    <link rel="canonical" href="${escapeHtml(canonical)}" />`,
    `    <meta property="og:site_name" content="${escapeHtml(seo.siteName)}" />`,
    `    <meta property="og:locale" content="${escapeHtml(seo.defaultLocale)}" />`,
    `    <meta property="og:type" content="${escapeHtml(route?.type ?? 'website')}" />`,
    `    <meta property="og:title" content="${escapeHtml(title)}" />`,
    `    <meta property="og:description" content="${escapeHtml(description)}" />`,
    `    <meta property="og:url" content="${escapeHtml(canonical)}" />`,
    `    <meta property="og:image" content="${escapeHtml(image)}" />`,
    '    <meta property="og:image:width" content="1200" />',
    '    <meta property="og:image:height" content="630" />',
    '    <meta name="twitter:card" content="summary_large_image" />',
    `    <meta name="twitter:title" content="${escapeHtml(title)}" />`,
    `    <meta name="twitter:description" content="${escapeHtml(description)}" />`,
    `    <meta name="twitter:image" content="${escapeHtml(image)}" />`,
  ]
  if (googleVerification) {
    lines.push(`    <meta name="google-site-verification" content="${escapeHtml(googleVerification)}" />`)
  }
  if (yandexVerification) {
    lines.push(`    <meta name="yandex-verification" content="${escapeHtml(yandexVerification)}" />`)
  }
  if (jsonLd) {
    lines.push(`    <script type="application/ld+json">${jsonLd.replaceAll('</script', '<\\/script')}</script>`)
  }
  lines.push(`    <link rel="manifest" href="/site.webmanifest?v=${iconAssetVersion}" />`)
  lines.push(`    <link rel="icon" href="/favicon.ico?v=${iconAssetVersion}" sizes="any" />`)
  lines.push(`    <link rel="icon" type="image/svg+xml" href="/favicon.svg?v=${iconAssetVersion}" />`)
  lines.push(`    <link rel="apple-touch-icon" href="/icons/apple-touch-icon.png?v=${iconAssetVersion}" />`)
  lines.push('<!-- seo:managed:end -->')
  return lines.join('\n')
}

function injectHead(html, route, options) {
  const cleaned = html.replace(/<!-- seo:managed:start -->[\s\S]*?<!-- seo:managed:end -->\n?/m, '')
  return cleaned.replace(/\s*<title>[\s\S]*?<\/title>\s*/i, '\n').replace(
    '</head>',
    `${managedHead(route, options)}\n  </head>`,
  )
}

async function writeHtml(outputPath, route, options) {
  await mkdir(path.dirname(outputPath), { recursive: true })
  await writeFile(outputPath, injectHead(baseHtml, route, options), 'utf8')
}

for (const route of seo.indexableRoutes) {
  await writeHtml(routeOutputPath(route.path), route, { index: true })
}

await writeHtml(path.join(dist, 'noindex.html'), {
  path: '/',
  title: seo.defaultTitle,
  description: seo.defaultDescription,
  type: 'website',
}, { index: false })

const siteUrl = cleanSiteUrl(process.env.VITE_SITE_URL)
const sitemap = [
  '<?xml version="1.0" encoding="UTF-8"?>',
  '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
  ...seo.indexableRoutes.flatMap((route) => [
    '  <url>',
    `    <loc>${escapeHtml(absoluteUrl(siteUrl, route.path))}</loc>`,
    `    <lastmod>${today}</lastmod>`,
    `    <changefreq>${escapeHtml(route.changefreq)}</changefreq>`,
    `    <priority>${route.priority}</priority>`,
    '  </url>',
  ]),
  '</urlset>',
  '',
].join('\n')
await writeFile(path.join(dist, 'sitemap.xml'), sitemap, 'utf8')

const robots = [
  'User-agent: *',
  'Allow: /',
  'Disallow: /api/',
  'Disallow: /admin',
  'Disallow: /auth',
  'Disallow: /verify-email',
  'Disallow: /forgot-password',
  'Disallow: /reset-password',
  'Disallow: /payment/',
  'Disallow: /feedback/',
  'Disallow: /history',
  'Disallow: /reading',
  'Disallow: /result',
  'Disallow: /achievements',
  'Disallow: /profile',
  '',
  `Sitemap: ${absoluteUrl(siteUrl, '/sitemap.xml')}`,
  '',
].join('\n')
await writeFile(path.join(dist, 'robots.txt'), robots, 'utf8')

const manifest = {
  name: seo.siteName,
  short_name: seo.siteName,
  description: seo.defaultDescription,
  lang: 'ru',
  start_url: '/',
  scope: '/',
  display: 'standalone',
  background_color: '#0b0618',
  theme_color: '#0b0618',
  icons: [
    { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any maskable' },
    { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any maskable' },
  ],
}
await writeFile(path.join(dist, 'site.webmanifest'), `${JSON.stringify(manifest, null, 2)}\n`, 'utf8')
