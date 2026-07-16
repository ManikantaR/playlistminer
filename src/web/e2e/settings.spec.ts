import { test, expect } from '@playwright/test';

const playlists = [
  { id: 1, youTubeId: 'PL0001', name: 'Incoming', description: null, isInbox: true, itemCount: 4 },
  { id: 2, youTubeId: 'PL0002', name: 'Programming', description: null, isInbox: false, itemCount: 9 },
];

test.describe('Settings page', () => {
  test('shows current inbox and allows selecting another playlist', async ({ page }) => {
    let postCalled = false;

    await page.route('**/api/**', async (route) => {
      const url = route.request().url();
      const method = route.request().method();

      if (method === 'GET' && url.endsWith('/api/oauth/status')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ connected: true }),
        });
        return;
      }

      if (method === 'GET' && url.endsWith('/api/sync/status')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ isRunning: false, lastSync: null }),
        });
        return;
      }

      if (method === 'GET' && url.endsWith('/api/playlists')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(playlists),
        });
        return;
      }

      if (method === 'POST' && url.endsWith('/api/playlists/2/set-inbox')) {
        postCalled = true;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: 'null',
        });
        return;
      }

      await route.fallback();
    });

    await page.goto('/settings');

    await expect(page.getByText('Current inbox')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Incoming Playlist' })).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Incoming playlist' })).toHaveValue('2');

    await page.getByRole('button', { name: 'Set as Incoming' }).click();

    await expect.poll(() => postCalled).toBe(true);
  });
});
