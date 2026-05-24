import { defineConfig, devices } from '@playwright/test'

/**
 * Koot Playwright configuration.
 *
 * Base URLs are driven by env vars so the same config works locally,
 * in docker-compose, and in CI.
 *
 * Local docker-compose defaults:
 *   frontend → http://localhost:5173  (node dev server)
 *   API      → http://localhost:5024  (dotnet run)
 *
 * Production / nginx defaults (task spec):
 *   PLAYWRIGHT_BASE_URL=http://localhost:80
 *   PLAYWRIGHT_API_URL=http://localhost:5000
 */
const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:5173'
const apiURL  = process.env.PLAYWRIGHT_API_URL  ?? 'http://localhost:5024'

export default defineConfig({
  testDir: './tests',
  outputDir: 'test-results',
  fullyParallel: false,          // game-flow tests are stateful; run sequentially by default
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,                    // avoid DB conflicts between suites
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
  ],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'mobile-chrome',
      use: { ...devices['Pixel 5'] },
      // Only run player-facing tests on mobile (join + play pages)
      testMatch: ['**/player.spec.ts'],
    },
  ],
})

/** Export so helpers can import the API base URL. */
export { apiURL }
