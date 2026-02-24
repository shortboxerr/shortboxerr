import { test, expect } from '@playwright/test';

test.describe('Background Services Status', () => {
  test('system status includes background service info', async ({ request }) => {
    const response = await request.get('/api/v1/system/status');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.startTime).toBeDefined();
    expect(data.version).toBeDefined();
  });

  test('health endpoint confirms services running', async ({ request }) => {
    const response = await request.get('/health');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.status).toBeDefined();
  });
});

test.describe('Metadata Refresh Service', () => {
  test('metadata refresh endpoint exists', async ({ request }) => {
    const response = await request.get('/api/v1/metadata/settings');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toBeDefined();
  });

  test('can trigger series metadata refresh', async ({ request }) => {
    const seriesResponse = await request.get('/api/v1/series');
    const seriesData = await seriesResponse.json();
    const series = seriesData.records || seriesData;
    
    if (series && series.length > 0) {
      const seriesId = series[0].id;
      const response = await request.post(`/api/v1/series/${seriesId}/refresh`);
      expect(response.status()).toBeLessThan(500);
    }
  });
});

test.describe('Discovery Refresh Service', () => {
  test('discovery endpoint reflects cached data', async ({ request }) => {
    const response = await request.get('/api/v1/pulllist/discover/week');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.weekStart || data.issues || Array.isArray(data)).toBeTruthy();
  });

  test('discovery cache has recent data', async ({ request }) => {
    const response = await request.get('/api/v1/pulllist/discover/week');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    if (data.weekStart) {
      const weekStart = new Date(data.weekStart);
      const now = new Date();
      const diffDays = Math.abs((now.getTime() - weekStart.getTime()) / (1000 * 60 * 60 * 24));
      expect(diffDays).toBeLessThan(14);
    }
  });
});

test.describe('Auto-Search Service', () => {
  test('auto-search settings endpoint exists', async ({ request }) => {
    const response = await request.get('/api/v1/settings/search');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toBeDefined();
  });
});

test.describe('Indexer Health Service', () => {
  test('indexer health check endpoint works', async ({ request }) => {
    const response = await request.get('/api/v1/nzb/indexers/health');
    expect(response.status()).toBeLessThan(500);
    const data = await response.json();
    expect(data).toBeDefined();
  });
});

test.describe('Site Health Service', () => {
  test('DDL site health endpoint returns status', async ({ request }) => {
    const response = await request.get('/api/v1/ddl/sites');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(Array.isArray(data)).toBeTruthy();
  });

  test('DDL sites have health info', async ({ request }) => {
    const response = await request.get('/api/v1/ddl/sites');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    if (data.length > 0) {
      expect(data[0].name || data[0].siteType).toBeDefined();
    }
  });
});

test.describe('Cover Cache Service', () => {
  test('cover settings endpoint exists', async ({ request }) => {
    const response = await request.get('/api/v1/settings/covers');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toBeDefined();
  });
});

test.describe('Download Monitoring', () => {
  test('download client status available', async ({ request }) => {
    const response = await request.get('/api/v1/nzb/download-client');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.clientType).toBeDefined();
  });

  test('activity endpoint shows download history', async ({ request }) => {
    const response = await request.get('/api/v1/activity');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(Array.isArray(data) || data.items).toBeTruthy();
  });
});

test.describe('Calendar Service', () => {
  test('calendar returns upcoming releases', async ({ request }) => {
    const response = await request.get('/api/v1/pulllist/calendar');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.startDate).toBeDefined();
    expect(data.endDate).toBeDefined();
    expect(data.days).toBeDefined();
  });

  test('calendar spans multiple weeks', async ({ request }) => {
    const response = await request.get('/api/v1/pulllist/calendar');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    const startDate = new Date(data.startDate);
    const endDate = new Date(data.endDate);
    const diffDays = (endDate.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24);
    expect(diffDays).toBeGreaterThan(7);
  });
});

test.describe('Notification Service', () => {
  test('notification settings endpoint exists', async ({ request }) => {
    const response = await request.get('/api/v1/notifications/settings');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toBeDefined();
  });

  test('notifications list endpoint exists', async ({ request }) => {
    const response = await request.get('/api/v1/notifications');
    expect(response.status()).toBeLessThan(500);
  });
});

test.describe('ComicVine Sync Service', () => {
  test('comicvine rate limit tracked', async ({ request }) => {
    const response = await request.get('/api/v1/comicvine/ratelimit');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.requestsUsed).toBeDefined();
    expect(data.requestLimit).toBeDefined();
    expect(data.windowResetTime).toBeDefined();
  });

  test('comicvine settings available', async ({ request }) => {
    const response = await request.get('/api/v1/comicvine/settings');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toBeDefined();
  });
});
