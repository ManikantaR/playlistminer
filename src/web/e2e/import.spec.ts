import { test, expect } from '@playwright/test';

test.describe('Import page', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/sync/status**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isRunning: false, lastSync: null }) }),
    );
    await page.route('**/api/sync/history**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
    );
  });

  test('shows drag-and-drop zone', async ({ page }) => {
    await page.goto('/import');
    await expect(page.getByText(/drag and drop/i)).toBeVisible();
  });

  test('shows import button', async ({ page }) => {
    await page.goto('/import');
    await expect(page.getByRole('button', { name: /import/i })).toBeVisible();
  });
});
