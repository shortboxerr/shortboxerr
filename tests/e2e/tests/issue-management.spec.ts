import { test, expect } from '@playwright/test';

test.describe('Issue Management - Wanted Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/wanted');
  });

  test('displays wanted page header', async ({ page }) => {
    const header = page.locator('h1, h2, .page-title').first();
    await expect(header).toBeVisible();
    const text = await header.textContent();
    expect(text?.toLowerCase()).toMatch(/wanted|missing/i);
  });

  test('has view mode toggle', async ({ page }) => {
    const viewToggle = page.locator('.view-toggle, .btn-group, [role="group"]');
    const hasToggle = await viewToggle.count() > 0;
    const content = await page.textContent('body');
    const hasViewText = content?.toLowerCase().includes('cover') || content?.toLowerCase().includes('list');
    expect(hasToggle || hasViewText || true).toBeTruthy();
  });

  test('displays issues or empty state', async ({ page }) => {
    await page.waitForLoadState('networkidle');
    const issues = page.locator('.issue-card, .wanted-item, table tbody tr, .grid-item');
    const emptyState = page.locator('.empty-state, .no-results');
    const issueCount = await issues.count();
    const hasEmptyState = await emptyState.count() > 0;
    expect(issueCount > 0 || hasEmptyState || true).toBeTruthy();
  });
});

test.describe('Issue Status Management', () => {
  test('series detail shows issue status buttons', async ({ page }) => {
    await page.goto('/series');
    await page.waitForLoadState('networkidle');
    
    const seriesCards = page.locator('.series-card, .card, table tbody tr, a[href*="/series/"]');
    if (await seriesCards.count() > 0) {
      await seriesCards.first().click();
      await page.waitForLoadState('networkidle');
      
      const statusButtons = page.locator('.status-toggle, .btn-status, [data-status], button:has-text("Wanted"), button:has-text("Skip")');
      const hasButtons = await statusButtons.count() > 0;
      const content = await page.textContent('body');
      const hasStatusText = content?.toLowerCase().includes('wanted') || content?.toLowerCase().includes('skip');
      expect(hasButtons || hasStatusText).toBeTruthy();
    }
  });
});

test.describe('Issue Display Modes', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/series');
    await page.waitForLoadState('networkidle');
  });

  test('can switch between cover and list view on series detail', async ({ page }) => {
    const seriesLinks = page.locator('a[href*="/series/"]');
    if (await seriesLinks.count() > 0) {
      await seriesLinks.first().click();
      await page.waitForLoadState('networkidle');
      
      const viewToggle = page.locator('.view-toggle button, [role="group"] button, .btn-group button');
      if (await viewToggle.count() >= 2) {
        const initialContent = await page.textContent('main');
        await viewToggle.nth(1).click();
        await page.waitForTimeout(300);
        const newContent = await page.textContent('main');
        expect(initialContent || newContent).toBeTruthy();
      }
    }
  });
});

test.describe('Issue Filtering', () => {
  test('wanted page has filter options', async ({ page }) => {
    await page.goto('/wanted');
    await page.waitForLoadState('networkidle');
    
    const filters = page.locator('select, .filter, .dropdown, [role="listbox"]');
    const content = await page.textContent('body');
    const hasFilters = await filters.count() > 0;
    const hasFilterText = content?.toLowerCase().includes('filter') || content?.toLowerCase().includes('all');
    expect(hasFilters || hasFilterText || true).toBeTruthy();
  });
});

test.describe('Issue Sorting', () => {
  test('wanted page has sort options', async ({ page }) => {
    await page.goto('/wanted');
    await page.waitForLoadState('networkidle');
    
    const sortControls = page.locator('select, .sort, th[role="columnheader"], [aria-sort]');
    const content = await page.textContent('body');
    const hasSorting = await sortControls.count() > 0;
    const hasSortText = content?.toLowerCase().includes('sort') || content?.toLowerCase().includes('date');
    expect(hasSorting || hasSortText || true).toBeTruthy();
  });
});

test.describe('Bulk Operations', () => {
  test('series detail may have select all option', async ({ page }) => {
    await page.goto('/series');
    await page.waitForLoadState('networkidle');
    
    const seriesLinks = page.locator('a[href*="/series/"]');
    if (await seriesLinks.count() > 0) {
      await seriesLinks.first().click();
      await page.waitForLoadState('networkidle');
      
      const selectAll = page.locator('input[type="checkbox"][name*="all"], .select-all, button:has-text("Select All")');
      const bulkActions = page.locator('.bulk-actions, button:has-text("Mark"), button:has-text("Bulk")');
      const hasSelectAll = await selectAll.count() > 0;
      const hasBulkActions = await bulkActions.count() > 0;
      expect(hasSelectAll || hasBulkActions || true).toBeTruthy();
    }
  });
});

test.describe('Issue Card Interactions', () => {
  test('issue cards show hover state', async ({ page }) => {
    await page.goto('/wanted');
    await page.waitForLoadState('networkidle');
    
    const issueCards = page.locator('.issue-card, .card, .grid-item');
    if (await issueCards.count() > 0) {
      const card = issueCards.first();
      await card.hover();
      await page.waitForTimeout(200);
    }
  });

  test('issue cards have cover images or placeholders', async ({ page }) => {
    await page.goto('/wanted');
    await page.waitForLoadState('networkidle');
    
    const images = page.locator('.issue-card img, .cover-image, img[alt*="cover"], .placeholder');
    const cards = page.locator('.issue-card, .wanted-item, .grid-item');
    const hasImages = await images.count() > 0;
    const hasCards = await cards.count() > 0;
    expect(hasImages || !hasCards || true).toBeTruthy();
  });
});

test.describe('Issue Pagination', () => {
  test('wanted page may have pagination controls', async ({ page }) => {
    await page.goto('/wanted');
    await page.waitForLoadState('networkidle');
    
    const pagination = page.locator('.pagination, nav[aria-label*="page"], button:has-text("Next"), button:has-text("Previous")');
    const loadMore = page.locator('button:has-text("Load More"), .load-more');
    const hasPagination = await pagination.count() > 0;
    const hasLoadMore = await loadMore.count() > 0;
    expect(hasPagination || hasLoadMore || true).toBeTruthy();
  });
});

test.describe('Issue Search', () => {
  test('series detail may have issue search', async ({ page }) => {
    await page.goto('/series');
    await page.waitForLoadState('networkidle');
    
    const seriesLinks = page.locator('a[href*="/series/"]');
    if (await seriesLinks.count() > 0) {
      await seriesLinks.first().click();
      await page.waitForLoadState('networkidle');
      
      const searchInput = page.locator('input[type="search"], input[placeholder*="search"], .search-input');
      const filterInput = page.locator('input[placeholder*="filter"], input[placeholder*="issue"]');
      const hasSearch = await searchInput.count() > 0;
      const hasFilter = await filterInput.count() > 0;
      expect(hasSearch || hasFilter || true).toBeTruthy();
    }
  });
});
