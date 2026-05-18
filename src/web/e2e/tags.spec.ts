import { test, expect } from '@playwright/test';

const mockTags = [
  { id: 1, name: 'Science', slug: 'science', category: 'Academic', videoCount: 15 },
  { id: 2, name: 'JavaScript', slug: 'javascript', category: 'Programming', videoCount: 42 },
];

test.describe('Tags page', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/tags**', (route) => {
      if (route.request().method() === 'GET') {
        route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockTags) });
      } else {
        route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ id: 3, name: 'NewTag', slug: 'newtag', category: null, videoCount: 0 }) });
      }
    });
    await page.route('**/api/sync/status**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isRunning: false, lastSync: null }) }),
    );
    await page.route('**/api/sync/history**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
    );
  });

  test('shows tag names', async ({ page }) => {
    await page.goto('/tags');
    await expect(page.getByText('Science')).toBeVisible();
    await expect(page.getByText('JavaScript')).toBeVisible();
  });

  test('groups tags by category', async ({ page }) => {
    await page.goto('/tags');
    await expect(page.getByText('Academic')).toBeVisible();
    await expect(page.getByText('Programming')).toBeVisible();
  });

  test('shows video count per tag', async ({ page }) => {
    await page.goto('/tags');
    await expect(page.getByText('15 videos')).toBeVisible();
    await expect(page.getByText('42 videos')).toBeVisible();
  });
});
