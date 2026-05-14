import { defineConfig, devices } from '@playwright/test';

const DEMO_URL = process.env.CAPNET_DEMO_URL || 'http://localhost:5500';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
  ],
  timeout: 60_000,

  use: {
    baseURL: DEMO_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  webServer: {
    command: 'dotnet run --project ..\\CapNet.Demo.Web -- "http://localhost:5500/"',
    url: DEMO_URL,
    reuseExistingServer: true,
    timeout: 60_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
