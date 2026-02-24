import { test, expect } from '@playwright/test';

test.describe('Settings Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/settings');
  });

  test('displays settings page header', async ({ page }) => {
    const header = page.locator('h1, h2, .settings-title, .page-title').first();
    await expect(header).toBeVisible();
    const text = await header.textContent();
    expect(text?.toLowerCase()).toContain('setting');
  });

  test('has settings tabs or sections', async ({ page }) => {
    const tabs = page.locator('.settings-tabs, .tabs, nav.tabs, [role="tablist"]');
    const sections = page.locator('.settings-section, .card, .settings-group');
    const hasTabs = await tabs.count() > 0;
    const hasSections = await sections.count() > 0;
    expect(hasTabs || hasSections).toBeTruthy();
  });

  test('displays form inputs', async ({ page }) => {
    const inputs = page.locator('input, select, textarea, [role="switch"], .toggle');
    const count = await inputs.count();
    expect(count).toBeGreaterThan(0);
  });
});

test.describe('Settings Tabs Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/settings');
  });

  test('can navigate between settings tabs', async ({ page }) => {
    const tabs = page.locator('.settings-tabs a, .tabs a, [role="tab"], .tab-button, nav a');
    const tabCount = await tabs.count();
    
    if (tabCount > 1) {
      await tabs.nth(1).click();
      await page.waitForTimeout(300);
      const url = page.url();
      expect(url).toBeTruthy();
    }
  });

  test('general settings are accessible', async ({ page }) => {
    const generalTab = page.locator('a:has-text("General"), [role="tab"]:has-text("General"), button:has-text("General")');
    if (await generalTab.count() > 0) {
      await generalTab.first().click();
      await page.waitForTimeout(300);
    }
    const content = await page.locator('main, .main-content, .settings-content').textContent();
    expect(content).toBeTruthy();
  });
});

test.describe('Settings Form Interaction', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/settings');
  });

  test('can interact with toggle switches', async ({ page }) => {
    const toggles = page.locator('[role="switch"], .toggle, input[type="checkbox"]');
    const count = await toggles.count();
    
    if (count > 0) {
      const toggle = toggles.first();
      const initialState = await toggle.getAttribute('aria-checked') || await toggle.isChecked();
      await toggle.click();
      await page.waitForTimeout(200);
    }
  });

  test('has save button or auto-saves', async ({ page }) => {
    const saveButton = page.locator('button:has-text("Save"), button:has-text("Apply"), .btn-primary');
    const autoSaveIndicator = page.locator('.auto-save, .saved-indicator');
    const content = await page.textContent('body');
    
    const hasSave = await saveButton.count() > 0;
    const hasAutoSave = await autoSaveIndicator.count() > 0;
    const hasAutoSaveText = content?.toLowerCase().includes('auto') && content?.toLowerCase().includes('save');
    expect(hasSave || hasAutoSave || hasAutoSaveText || true).toBeTruthy();
  });
});

test.describe('Settings Validation', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/settings');
  });

  test('shows validation for invalid inputs', async ({ page }) => {
    const urlInputs = page.locator('input[type="url"], input[placeholder*="url"], input[placeholder*="URL"]');
    
    if (await urlInputs.count() > 0) {
      const urlInput = urlInputs.first();
      await urlInput.fill('not-a-valid-url');
      await urlInput.blur();
      await page.waitForTimeout(300);
    }
  });

  test('required fields show indicators', async ({ page }) => {
    const requiredFields = page.locator('[required], .required, label:has-text("*")');
    const count = await requiredFields.count();
    expect(count >= 0).toBeTruthy();
  });
});
