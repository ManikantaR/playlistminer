import { test, expect } from '@playwright/test';

const makeVideos = (items: { id: number; title: string }[]) => ({
  items: items.map((v) => ({
    ...v,
    youTubeId: `yt${v.id}`,
    channelName: 'Channel',
    thumbnailUrl: '',
    duration: 'PT5M',
    publishedAt: '2024-01-01T00:00:00Z',
    status: 'Active',
    tags: [],
  })),
  totalCount: items.length,
  page: 1,
  pageSize: 20,
  totalPages: 1,
});

test.describe('Search functionality', () => {
  test('shows search input on videos page', async ({ page }) => {
    await page.route('**/api/videos**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(makeVideos([])) }),
    );
    await page.route('**/api/tags**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
    );
    await page.goto('/videos');
    await expect(page.getByRole('searchbox')).toBeVisible();
  });

  test('search input accepts text', async ({ page }) => {
    await page.route('**/api/videos**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(makeVideos([{ id: 1, title: 'TypeScript' }])) }),
    );
    await page.route('**/api/tags**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
    );
    await page.goto('/videos');
    const searchbox = page.getByRole('searchbox');
    await searchbox.fill('TypeScript');
    await expect(searchbox).toHaveValue('TypeScript');
  });

  test('clear button appears when searching', async ({ page }) => {
    await page.route('**/api/videos**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(makeVideos([])) }),
    );
    await page.route('**/api/tags**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
    );
    await page.goto('/videos');
    await page.getByRole('searchbox').fill('test');
    await expect(page.getByRole('button', { name: /clear/i })).toBeVisible();
  });

  test('status filter dropdown works', async ({ page }) => {
    await page.route('**/api/videos**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(makeVideos([])) }),
    );
    await page.route('**/api/tags**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
    );
    await page.goto('/videos');
    const select = page.getByLabel('Filter by status');
    await select.selectOption('Active');
    await expect(select).toHaveValue('Active');
  });
});
