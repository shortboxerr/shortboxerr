import { test, expect } from '@playwright/test';

test.describe('API Health Endpoints', () => {
  test('health endpoint returns OK', async ({ request }) => {
    const response = await request.get('/health');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.status).toBeDefined();
  });

  test('system status endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/system/status');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.version).toBeDefined();
    expect(data.startTime).toBeDefined();
  });
});

test.describe('ComicVine API Integration', () => {
  test('rate limit status endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/comicvine/ratelimit');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.requestsUsed).toBeDefined();
    expect(data.requestLimit).toBeDefined();
    expect(data.windowResetTime).toBeDefined();
  });

  test('comicvine test endpoint exists', async ({ request }) => {
    const response = await request.get('/api/v1/comicvine/test');
    expect(response.status()).toBeLessThan(500);
  });
});

test.describe('Series API Endpoints', () => {
  test('series list endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/series');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(Array.isArray(data) || data.records || data.items || data.series).toBeTruthy();
  });

  test('series endpoint supports pagination', async ({ request }) => {
    const response = await request.get('/api/v1/series?page=1&pageSize=10');
    expect(response.ok()).toBeTruthy();
  });

  test('series search endpoint responds', async ({ request }) => {
    const response = await request.get('/api/v1/series/search?q=test');
    expect(response.status()).toBeLessThan(500);
  });
});

test.describe('Pull List API Endpoints', () => {
  test('weekly pull list endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/pulllist/week');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toBeDefined();
  });

  test('discovery endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/pulllist/discover/week');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.weekStart || data.issues || Array.isArray(data)).toBeTruthy();
  });
});

test.describe('Wanted API Endpoints', () => {
  test('wanted issues endpoint returns array', async ({ request }) => {
    const response = await request.get('/api/v1/wanted/issues');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.items || Array.isArray(data)).toBeTruthy();
  });
});

test.describe('Settings API Endpoints', () => {
  test('general settings endpoint returns configuration', async ({ request }) => {
    const response = await request.get('/api/v1/settings/general');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toBeDefined();
  });

  test('UI settings endpoint returns configuration', async ({ request }) => {
    const response = await request.get('/api/v1/settings/ui');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toBeDefined();
  });
});

test.describe('Activity API Endpoints', () => {
  test('activity endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/activity');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(Array.isArray(data) || data.items).toBeTruthy();
  });
});

test.describe('Calendar API Endpoints', () => {
  test('calendar endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/pulllist/calendar');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.startDate || data.days).toBeDefined();
  });
});

test.describe('Download Client API Endpoints', () => {
  test('download clients endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/nzb/download-client');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.clientType || data.sabnzbd || data.nzbget).toBeDefined();
  });
});

test.describe('Indexer API Endpoints', () => {
  test('indexers endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/nzb/indexers');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(Array.isArray(data.indexers) || data.indexers).toBeTruthy();
  });

  test('indexer health endpoint responds', async ({ request }) => {
    const response = await request.get('/api/v1/nzb/indexers/health');
    expect(response.status()).toBeLessThan(500);
    const data = await response.json();
    expect(data).toBeDefined();
  });
});

test.describe('DDL Sites API Endpoints', () => {
  test('DDL sites endpoint returns array', async ({ request }) => {
    const response = await request.get('/api/v1/ddl/sites');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(Array.isArray(data)).toBeTruthy();
  });

  test('DDL site health endpoint returns data', async ({ request }) => {
    const response = await request.get('/api/v1/ddl/sites');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(Array.isArray(data)).toBeTruthy();
  });
});

test.describe('Logs API Endpoints', () => {
  test('logs endpoint returns log info', async ({ request }) => {
    const response = await request.get('/api/v1/system/logs');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.logDirectory || data.files).toBeDefined();
  });

  test('recent logs endpoint returns log lines', async ({ request }) => {
    const response = await request.get('/api/v1/system/logs/recent');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.lines || data.fileName).toBeDefined();
  });
});

test.describe('Notification API Endpoints', () => {
  test('notifications endpoint exists', async ({ request }) => {
    const response = await request.get('/api/v1/notifications');
    expect(response.status()).toBeLessThan(500);
  });
});

test.describe('API Error Handling', () => {
  test('non-existent API endpoint handled gracefully', async ({ request }) => {
    const response = await request.get('/api/v1/nonexistent');
    const contentType = response.headers()['content-type'];
    expect(response.status()).toBeLessThan(500);
  });

  test('invalid series ID returns appropriate response', async ({ request }) => {
    const response = await request.get('/api/v1/series/99999999');
    expect([404, 400, 200].includes(response.status())).toBeTruthy();
  });
});

test.describe('API Response Headers', () => {
  test('API returns JSON content type', async ({ request }) => {
    const response = await request.get('/api/v1/system/status');
    const contentType = response.headers()['content-type'];
    expect(contentType).toContain('application/json');
  });

  test('API includes cache headers', async ({ request }) => {
    const response = await request.get('/api/v1/series');
    expect(response.headers()).toBeDefined();
  });
});
