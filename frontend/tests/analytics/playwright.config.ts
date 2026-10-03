import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: '.',
  testMatch: 'metrika.e2e.ts',
  outputDir: '../../test-results/analytics',
  fullyParallel: true,
  reporter: 'line',
  use: {
    baseURL: 'http://127.0.0.1:4184',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile-chromium', use: { ...devices['Pixel 5'] } },
  ],
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 4184 --strictPort',
    cwd: '../..',
    env: { VITE_YANDEX_METRICA_ID: '12345678', VITE_API_URL: '' },
    url: 'http://127.0.0.1:4184',
    reuseExistingServer: false,
  },
})
