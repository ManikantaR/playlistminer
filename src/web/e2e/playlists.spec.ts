import { test, expect } from '@playwright/test';

const playlists = [
  { id: 1, youTubeId: 'PL0001', name: 'Incoming', description: null, isInbox: true, itemCount: 4 },
  { id: 2, youTubeId: 'PL0002', name: 'Programming', description: null, isInbox: false, itemCount: 9 },
];

test.describe('Playlists page', () => {
  test('shows current inbox and allows selecting another playlist', async ({ page }) => {
    let postCalled = false;

    await page.route('**/api/playlists**', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(playlists),
        });
        return;
      }

      if (route.request().method() === 'POST' && route.request().url().endsWith('/api/playlists/2/set-inbox')) {
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

    await page.goto('/playlists');

    await expect(page.getByText('Incoming')).toBeVisible();
    await expect(page.getByText('Inbox', { exact: true })).toBeVisible();

    await page.getByRole('button', { name: /set programming as inbox/i }).click();

    await expect.poll(() => postCalled).toBe(true);
  });
});
