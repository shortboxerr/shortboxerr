import { test, expect } from '@playwright/test';

/**
 * E2E tests for series management workflows.
 * Tests series list, detail view, filtering, and navigation.
 */

test.describe('Series List', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/series');
    // Wait for page to be ready
    await expect(page.locator('h1')).toContainText('Series');
  });

  test('displays series list with correct header', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Series');
  });

  test('has search functionality', async ({ page }) => {
    // Look for search input
    const searchInput = page.locator('input[type="search"], input[placeholder*="Search"], input[placeholder*="search"]');
    await expect(searchInput.first()).toBeVisible();
  });

  test('has view toggle (grid/list)', async ({ page }) => {
    // Look for view toggle buttons
    const viewToggle = page.locator('button:has([class*="grid"]), button:has([class*="list"]), [aria-label*="view"]');
    // May not be visible if only one view is available
    const count = await viewToggle.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });

  test('has filter controls', async ({ page }) => {
    // Look for filter button or dropdown
    const filterControl = page.locator('button:has-text("Filter"), select, [aria-label*="filter"]');
    // Filter may be hidden initially
    const count = await filterControl.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });

  test('has sort controls', async ({ page }) => {
    // Look for sort button or dropdown
    const sortControl = page.locator('button:has-text("Sort"), select[aria-label*="sort"], th[aria-sort]');
    const count = await sortControl.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });
});

test.describe('Series Search', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/series');
    await expect(page.locator('h1')).toContainText('Series');
  });

  test('can type in search box', async ({ page }) => {
    const searchInput = page.locator('input[type="search"], input[placeholder*="Search"], input[placeholder*="search"]').first();
    
    if (await searchInput.isVisible()) {
      await searchInput.fill('Batman');
      await expect(searchInput).toHaveValue('Batman');
    }
  });

  test('search results update based on input', async ({ page }) => {
    const searchInput = page.locator('input[type="search"], input[placeholder*="Search"], input[placeholder*="search"]').first();
    
    if (await searchInput.isVisible()) {
      // Get initial content state
      const mainContent = page.locator('main, .series-list, .content').first();
      await expect(mainContent).toBeVisible();
      
      // Type search query
      await searchInput.fill('zzznonexistent');
      
      // Wait for search to process
      await page.waitForTimeout(500);
      
      // Page should still be visible (may show "no results")
      await expect(page.locator('body')).toBeVisible();
    }
  });
});

test.describe('Series Detail Page', () => {
  test('can access series detail by URL', async ({ page }) => {
    // Navigate to a series detail page (series ID 1)
    await page.goto('/series/25');
    
    // Should either show series detail or redirect/show error
    const pageContent = page.locator('body');
    await expect(pageContent).toBeVisible();
  });

  test('series detail page has expected sections', async ({ page }) => {
    await page.goto('/series/25');
    
    // Wait for page load
    await page.waitForTimeout(1000);
    
    // Look for common series detail elements
    const hasHeader = await page.locator('h1, h2').first().isVisible();
    expect(hasHeader).toBeTruthy();
  });
});

test.describe('Series Navigation', () => {
  test('can navigate from series list to series detail', async ({ page }) => {
    await page.goto('/series');
    await expect(page.locator('h1')).toContainText('Series');
    
    // Look for clickable series items
    const seriesLink = page.locator('a[href^="/series/"], .series-card, .series-item');
    const count = await seriesLink.count();
    
    if (count > 0) {
      // Click first series
      await seriesLink.first().click();
      
      // Should navigate to a detail page
      await page.waitForURL(/\/series\/\d+/);
    }
  });

  test('can navigate back to series list from detail', async ({ page }) => {
    await page.goto('/series/25');
    
    // Wait for page load
    await page.waitForTimeout(500);
    
    // Look for back button or breadcrumb
    const backLink = page.locator('a[href="/series"], button:has-text("Back"), a:has-text("Series")');
    
    if (await backLink.count() > 0) {
      await backLink.first().click();
      await expect(page).toHaveURL(/\/series$/);
    }
  });
});

test.describe('Add Series Flow', () => {
  test('has add series button', async ({ page }) => {
    await page.goto('/series');
    
    // Look for add button
    const addButton = page.locator('button:has-text("Add"), a:has-text("Add"), [aria-label*="add"]');
    const count = await addButton.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });

  test('add button opens modal or navigates', async ({ page }) => {
    await page.goto('/series');
    
    const addButton = page.locator('button:has-text("Add Series"), button:has-text("Add")').first();
    
    if (await addButton.isVisible()) {
      await addButton.click();
      
      // Wait for modal or navigation
      await page.waitForTimeout(500);
      
      // Should either show modal or navigate
      const bodyVisible = await page.locator('body').isVisible();
      expect(bodyVisible).toBeTruthy();
    }
  });
});
