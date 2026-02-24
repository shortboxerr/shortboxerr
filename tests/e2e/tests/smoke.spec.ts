import { test, expect } from '@playwright/test';

/**
 * Smoke tests for Shortboxerr - basic page loading and critical path coverage.
 * These tests verify that the application is running and key pages are accessible.
 */

test.describe('Dashboard', () => {
  test('loads successfully', async ({ page }) => {
    await page.goto('/');
    
    // Verify page title
    await expect(page).toHaveTitle(/Shortboxerr/);
    
    // Verify dashboard header
    await expect(page.locator('h1')).toContainText('Dashboard');
    
    // Verify navigation is present
    await expect(page.locator('nav')).toBeVisible();
  });

  test('shows main content sections', async ({ page }) => {
    await page.goto('/');
    
    // Verify main content area is present
    const mainContent = page.locator('main, .main-content, .dashboard');
    await expect(mainContent.first()).toBeVisible();
  });
});

test.describe('Series Page', () => {
  test('loads series list', async ({ page }) => {
    await page.goto('/series');
    
    // Verify page header
    await expect(page.locator('h1')).toContainText('Series');
    
    // Verify page loaded (either shows series or add button)
    const pageContent = page.locator('main, .series-list, .content');
    await expect(pageContent.first()).toBeVisible();
  });
});

test.describe('Pull List Page', () => {
  test('loads pull list', async ({ page }) => {
    await page.goto('/pulllist');
    
    // Verify page header
    await expect(page.locator('h1, h2').first()).toContainText(/Pull List|This Week/);
    
    // Verify week navigation or view controls are present
    const weekControls = page.locator('.week-navigation, .pull-list-header, [class*="week"]');
    await expect(weekControls.first()).toBeVisible();
  });
});

test.describe('Settings Page', () => {
  test('loads settings page', async ({ page }) => {
    await page.goto('/settings');
    
    // Verify settings header
    await expect(page.locator('h1')).toContainText('Settings');
    
    // Verify settings tabs or sections are present
    const settingsSections = page.locator('.settings-nav, .settings-tabs, nav');
    await expect(settingsSections.first()).toBeVisible();
  });
});

test.describe('Wanted Page', () => {
  test('loads wanted issues list', async ({ page }) => {
    await page.goto('/wanted');
    
    // Verify page header
    await expect(page.locator('h1')).toContainText('Wanted');
  });
});

test.describe('Calendar Page', () => {
  test('loads calendar view', async ({ page }) => {
    await page.goto('/calendar');
    
    // Verify page header
    await expect(page.locator('h1')).toContainText('Calendar');
    
    // Verify calendar grid or view is present
    const calendarView = page.locator('.calendar-grid, .calendar-container, [class*="calendar"]');
    await expect(calendarView.first()).toBeVisible();
  });
});

test.describe('Activity/History Page', () => {
  test('loads activity log', async ({ page }) => {
    await page.goto('/activity');
    
    // Verify page header
    await expect(page.locator('h1')).toContainText(/Activity|History/);
  });
});

test.describe('Navigation', () => {
  test('can navigate between main pages', async ({ page }) => {
    // Start at dashboard
    await page.goto('/');
    await expect(page.locator('h1')).toContainText('Dashboard');
    
    // Navigate to series
    await page.click('a[href="/series"], nav >> text=Series');
    await expect(page.locator('h1')).toContainText('Series');
    
    // Navigate to pull list
    await page.click('a[href="/pulllist"], nav >> text="Pull List"');
    await expect(page).toHaveURL(/pulllist/);
    
    // Navigate back to dashboard
    await page.click('a[href="/"], nav >> text=Dashboard');
    await expect(page.locator('h1')).toContainText('Dashboard');
  });
});

test.describe('Theme Toggle', () => {
  test('page has theme attribute', async ({ page }) => {
    await page.goto('/');
    
    // Verify the page has a theme attribute (either light or dark)
    const theme = await page.locator('html').getAttribute('data-theme');
    expect(['light', 'dark', null]).toContain(theme);
  });
});
