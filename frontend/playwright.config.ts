import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 45_000,
  workers: 1,
  use: {
    ...devices['Desktop Chrome'],
    baseURL: 'http://127.0.0.1:5174',
    trace: 'retain-on-failure',
  },
  reporter: 'list',
  webServer: [
    {
      command:
        'dotnet run --project ../backend/InvoiceManager.Api --configuration Release --no-launch-profile --urls http://127.0.0.1:5081',
      url: 'http://127.0.0.1:5081/api/health',
      env: {
        ConnectionStrings__Invoices: `Data Source=App_Data/e2e-${Date.now()}.db;Default Timeout=30`,
        Logging__LogLevel__Default: 'Warning',
      },
      timeout: 90_000,
    },
    {
      command: 'npm run dev -- --port 5174',
      url: 'http://127.0.0.1:5174',
      env: { API_PROXY_TARGET: 'http://127.0.0.1:5081' },
    },
  ],
});
