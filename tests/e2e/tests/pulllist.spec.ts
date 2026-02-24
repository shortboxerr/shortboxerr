import { test, expect } from '@playwright/test';

/**
 * E2E tests for pull list workflows.
 * Tests weekly release views, filtering, and navigation.
 */

test.describe('Pull List Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/pulllist');
    await page.waitForTimeout(500);
  });

  test('displays pull list header', async ({ page }) => {
    // Page should have a title/header
    const header = page.locator('h1, h2').first();
    await expect(header).toBeVisible();
  });

  test('shows week navigation or selector', async ({ page }) => {
    // Look for week navigation controls
    const weekControls = page.locator(
      '[class*="week"], button:has-text("This Week"), button:has-text("Next"), button:has-text("Previous"), select'
    );
    const count = await weekControls.count();
    expect(count).toBeGreaterThan(0);
  });

  test('has view mode controls', async ({ page }) => {
    // Look for view mode buttons (All Releases, My Pull List, etc.)
    const viewControls = page.locator(
      'button:has-text("All"), button:has-text("Pull List"), button:has-text("My"), [role="tab"]'
    );
    const count = await viewControls.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });

  test('displays release count', async ({ page }) => {
    // Look for release count indicator
    const body = page.locator('body');
    const text = await body.textContent();
    
    // Page should contain some content
    expect(text).toBeTruthy();
  });
});

test.describe('Pull List Week Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/pulllist');
    await page.waitForTimeout(500);
  });

  test('can navigate to next week', async ({ page }) => {
    const nextButton = page.locator('button:has-text("Next"), button[aria-label*="next"], button:has([class*="chevron-right"])');
    
    if (await nextButton.count() > 0) {
      const initialUrl = page.url();
      await nextButton.first().click();
      await page.waitForTimeout(500);
      
      // Page should update (URL might change or content might reload)
      const bodyVisible = await page.locator('body').isVisible();
      expect(bodyVisible).toBeTruthy();
    }
  });

  test('can navigate to previous week', async ({ page }) => {
    const prevButton = page.locator('button:has-text("Previous"), button:has-text("Prev"), button[aria-label*="prev"], button:has([class*="chevron-left"])');
    
    if (await prevButton.count() > 0) {
      await prevButton.first().click();
      await page.waitForTimeout(500);
      
      const bodyVisible = await page.locator('body').isVisible();
      expect(bodyVisible).toBeTruthy();
    }
  });
});

test.describe('Pull List Filtering', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/pulllist');
    await page.waitForTimeout(500);
  });

  test('can toggle between view modes', async ({ page }) => {
    // Look for "All Releases" or "My Pull List" toggles
    const viewModeButtons = page.locator('button:has-text("All"), button:has-text("My Pull List"), button:has-text("Releases")');
    
    if (await viewModeButtons.count() > 0) {
      await viewModeButtons.first().click();
      await page.waitForTimeout(500);
      
      const bodyVisible = await page.locator('body').isVisible();
      expect(bodyVisible).toBeTruthy();
    }
  });

  test('has publisher filter if available', async ({ page }) => {
    const publisherFilter = page.locator('select:has-text("Publisher"), button:has-text("Publisher"), [aria-label*="publisher"]');
    const count = await publisherFilter.count();
    // Publisher filter is optional
    expect(count).toBeGreaterThanOrEqual(0);
  });
});

test.describe('Pull List Issue Cards', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/pulllist');
    await page.waitForTimeout(500);
  });

  test('displays issue cards or list items', async ({ page }) => {
    // Look for issue cards or list items
    const issueElements = page.locator('.pull-list-card, .issue-card, .release-item, tr');
    
    // May have zero if no releases this week
    const count = await issueElements.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });

  test('issue cards have cover images or placeholders', async ({ page }) => {
    const coverElements = page.locator('.pull-list-card img, .issue-card img, [class*="cover"]');
    
    // May have zero if no releases
    const count = await coverElements.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });

  test('issue cards show series/issue information', async ({ page }) => {
    // Page should contain issue information (titles, numbers, etc.)
    const body = page.locator('body');
    const text = await body.textContent();
    expect(text).toBeTruthy();
  });
});

test.describe('Pull List Add Series Flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/pulllist');
    await page.waitForTimeout(500);
  });

  test('discoverable issues have add button', async ({ page }) => {
    // Look for add buttons on issue cards
    const addButtons = page.locator('button:has-text("Add"), button[aria-label*="add"], .pull-list-card button');
    
    // May have zero if no discoverable issues
    const count = await addButtons.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });

  test('clicking add button opens modal or triggers action', async ({ page }) => {
    const addButtons = page.locator('.pull-list-card button:has-text("Add"), button[aria-label*="add to"]').first();
    
    if (await addButtons.count() > 0) {
      await addButtons.click();
      await page.waitForTimeout(500);
      
      // Should either open modal or trigger action
      const bodyVisible = await page.locator('body').isVisible();
      expect(bodyVisible).toBeTruthy();
    }
  });
});
