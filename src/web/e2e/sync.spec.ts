import { test, expect } from '@playwright/test';

test.describe('Sync functionality', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/sync/status**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ isRunning: false, lastSync: '2024-01-01T10:00:00Z' }),
      }),
    );
    await page.route('**/api/sync/history**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 1, syncType: 'Full', startedAt: '2024-01-01T10:00:00Z', completedAt: '2024-01-01T10:05:00Z', videosProcessed: 42, status: 'Completed', errors: null },
        ]),
      }),
    );
    await page.route('**/api/videos**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }) }),
    );
    await page.route('**/api/tags**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
    );
  });

  test('shows sync history on dashboard', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByText('Full')).toBeVisible();
  });

  test('shows Sync Now button on dashboard', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('button', { name: /sync now/i })).toBeVisible();
  });

  test('triggers sync when button clicked', async ({ page }) => {
    let syncCalled = false;
    await page.route('**/api/sync/trigger**', (route) => {
      syncCalled = true;
      route.fulfill({ status: 204 });
    });
    await page.goto('/');
    await page.getByRole('button', { name: /sync now/i }).click();
    await expect.poll(() => syncCalled).toBe(true);
  });
});
