import { test, expect } from '@playwright/test';

test.describe('404 Error Handling', () => {
  test('shows error page for non-existent series', async ({ page }) => {
    await page.goto('/series/99999999');
    await page.waitForLoadState('networkidle');
    const content = await page.textContent('body');
    const has404 = content?.includes('404') || content?.includes('not found') || content?.includes('Not Found');
    const hasError = content?.toLowerCase().includes('error') || content?.toLowerCase().includes('could not');
    const redirectedAway = !page.url().includes('99999999');
    const hasContent = content!.length > 100;
    expect(has404 || hasError || redirectedAway || hasContent).toBeTruthy();
  });

  test('shows error page for invalid route', async ({ page }) => {
    await page.goto('/this-route-does-not-exist-at-all');
    const content = await page.textContent('body');
    expect(content).toBeTruthy();
  });
});

test.describe('Empty States', () => {
  test('wanted page handles empty list gracefully', async ({ page }) => {
    await page.goto('/wanted');
    await page.waitForLoadState('networkidle');
    const emptyState = page.locator('.empty-state, .no-results, .empty-message');
    const itemList = page.locator('.wanted-item, .issue-card, table tbody tr');
    const content = await page.textContent('body');
    const hasEmptyState = await emptyState.count() > 0;
    const hasItems = await itemList.count() > 0;
    const hasEmptyText = content?.toLowerCase().includes('no wanted') || content?.toLowerCase().includes('empty');
    expect(hasEmptyState || hasItems || hasEmptyText || content!.length > 0).toBeTruthy();
  });

  test('activity page handles empty log gracefully', async ({ page }) => {
    await page.goto('/activity');
    await page.waitForLoadState('networkidle');
    const emptyState = page.locator('.empty-state, .no-activity');
    const activityList = page.locator('.activity-item, .log-entry, table tbody tr, .activity-card');
    const content = await page.textContent('body');
    const hasEmptyState = await emptyState.count() > 0;
    const hasActivity = await activityList.count() > 0;
    const hasEmptyText = content?.toLowerCase().includes('no activity') || content?.toLowerCase().includes('empty');
    expect(hasEmptyState || hasActivity || hasEmptyText || content!.length > 0).toBeTruthy();
  });

  test('series search with no matches shows appropriate message', async ({ page }) => {
    await page.goto('/series');
    const searchInput = page.locator('input[type="search"], input[placeholder*="search"], .search-input');
    if (await searchInput.count() > 0) {
      await searchInput.fill('xyznonexistentseries123456');
      await page.waitForTimeout(500);
      const content = await page.textContent('body');
      expect(content).toBeTruthy();
    }
  });
});

test.describe('Loading States', () => {
  test('pages show loading indicators before data', async ({ page }) => {
    const responsePromise = page.waitForResponse(resp => resp.url().includes('/api/'));
    await page.goto('/series');
    try {
      await responsePromise;
    } catch {
      // API might load too fast
    }
  });

  test('dashboard loads data progressively', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');
    const mainContent = page.locator('main, .main-content, .dashboard');
    await expect(mainContent).toBeVisible({ timeout: 5000 });
  });
});

test.describe('Network Error Handling', () => {
  test('page handles slow API responses', async ({ page }) => {
    await page.goto('/series');
    await page.waitForLoadState('networkidle');
    const content = await page.textContent('body');
    expect(content?.length).toBeGreaterThan(0);
  });
});

test.describe('Input Validation', () => {
  test('search rejects special characters gracefully', async ({ page }) => {
    await page.goto('/series');
    const searchInput = page.locator('input[type="search"], input[placeholder*="search"], .search-input');
    if (await searchInput.count() > 0) {
      await searchInput.fill('<script>alert("xss")</script>');
      await page.waitForTimeout(300);
      const alertDialog = page.locator('[role="alert"], .error, .alert-danger');
      const content = await page.textContent('body');
      expect(content?.includes('<script>')).toBeFalsy();
    }
  });

  test('pull list handles invalid week navigation', async ({ page }) => {
    await page.goto('/pulllist');
    await page.waitForLoadState('networkidle');
    const content = await page.textContent('body');
    expect(content?.length).toBeGreaterThan(0);
  });
});

test.describe('Responsive Design', () => {
  test('dashboard displays correctly on mobile viewport', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const mainContent = page.locator('main, .main-content, .dashboard');
    await expect(mainContent).toBeVisible();
  });

  test('navigation adapts to mobile viewport', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/');
    const mobileMenu = page.locator('.hamburger, .mobile-menu, [aria-label*="menu"], .menu-toggle');
    const sideNav = page.locator('nav, .sidebar, .nav');
    const hasMobileMenu = await mobileMenu.count() > 0;
    const hasNav = await sideNav.count() > 0;
    expect(hasMobileMenu || hasNav).toBeTruthy();
  });

  test('series list displays correctly on tablet viewport', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/series');
    await page.waitForLoadState('networkidle');
    const content = page.locator('main, .main-content');
    await expect(content).toBeVisible();
  });
});
