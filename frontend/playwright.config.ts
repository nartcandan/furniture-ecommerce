import { defineConfig, devices } from '@playwright/test'

/** The E2E API and store. Their ports differ from development, so both can run side by side. */
export const E2E_API_URL = 'http://localhost:5069'
export const E2E_STORE_URL = 'http://localhost:5174'

const isCI = Boolean(process.env.CI)

/**
 * Browser tests of the main flows, against the real API and a real PostgreSQL. Each run starts with an empty
 * database and a test payment page in place of iyzico (the API's E2E environment, see appsettings.E2E.json).
 * Tests create the data they need through the API, so they do not depend on each other.
 */
export default defineConfig({
  testDir: './e2e',
  globalTeardown: './e2e/global-teardown.ts',
  // A purchase walks through several pages and the payment page, which takes longer than the default 30 s.
  timeout: 60_000,
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  workers: isCI ? 2 : undefined,
  reporter: isCI ? [['github'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: E2E_STORE_URL,
    locale: 'en-GB',
    timezoneId: 'Europe/Istanbul',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      // A fresh database first, then the API, which creates the tables and the admin account as it starts.
      // The API is never reused: a running one would still hold connections to the database just replaced.
      command:
        'docker compose -f ../docker-compose.e2e.yml up -d --wait --force-recreate && ' +
        'dotnet run --project ../backend/src/FurnitureEcommerce.Api --launch-profile e2e',
      url: `${E2E_API_URL}/api/categories`,
      reuseExistingServer: false,
      timeout: 240_000,
      stdout: 'ignore',
      stderr: 'pipe',
    },
    {
      // The built store, as it will be deployed. The dev server compiles each page on first use, which is slower.
      command: 'npm run build && npx vite preview --port 5174 --strictPort',
      url: E2E_STORE_URL,
      env: { VITE_API_URL: E2E_API_URL },
      reuseExistingServer: false,
      timeout: 120_000,
    },
  ],
})
