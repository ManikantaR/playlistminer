import { test, expect } from '@playwright/test';

const mockVideos = {
  items: [
    {
      id: 1,
      youTubeId: 'abc',
      title: 'TypeScript Tutorial',
      channelName: 'Code Channel',
      thumbnailUrl: '',
      duration: 'PT10M',
      publishedAt: '2024-01-01T00:00:00Z',
      status: 'Active',
      tags: [{ tagId: 1, tagName: 'TypeScript', source: 'Manual', confidence: null }],
    },
    {
      id: 2,
      youTubeId: 'def',
      title: 'React Patterns',
      channelName: 'Dev Academy',
      thumbnailUrl: '',
      duration: 'PT15M',
      publishedAt: '2024-02-01T00:00:00Z',
      status: 'Active',
      tags: [],
    },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

test.describe('Videos page', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/videos**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockVideos) }),
    );
    await page.route('**/api/tags**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
    );
    await page.route('**/api/sync/status**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isRunning: false, lastSync: null }) }),
    );
    await page.route('**/api/sync/history**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
    );
  });

  test('shows video titles', async ({ page }) => {
    await page.goto('/videos');
    await expect(page.getByText('TypeScript Tutorial')).toBeVisible();
    await expect(page.getByText('React Patterns')).toBeVisible();
  });

  test('shows channel names', async ({ page }) => {
    await page.goto('/videos');
    await expect(page.getByText('Code Channel')).toBeVisible();
  });

  test('shows tag badges', async ({ page }) => {
    await page.goto('/videos');
    await expect(page.getByText('TypeScript')).toBeVisible();
  });

  test('has search input', async ({ page }) => {
    await page.goto('/videos');
    await expect(page.getByRole('searchbox')).toBeVisible();
  });

  test('has status filter dropdown', async ({ page }) => {
    await page.goto('/videos');
    await expect(page.getByLabel('Filter by status')).toBeVisible();
  });

  test('has view links for each video', async ({ page }) => {
    await page.goto('/videos');
    const viewLinks = page.getByRole('link', { name: 'View' });
    await expect(viewLinks.first()).toBeVisible();
  });
});
