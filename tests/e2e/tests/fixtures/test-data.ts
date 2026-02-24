/**
 * Test data fixtures for E2E tests.
 * These can be used to seed the database with known data before tests.
 */

export const testSeries = {
  batman: {
    title: 'Batman',
    publisher: 'DC Comics',
    comicVineId: 796,
    startYear: 2016,
  },
  spiderMan: {
    title: 'Amazing Spider-Man',
    publisher: 'Marvel',
    comicVineId: 72051,
    startYear: 2018,
  },
  spawn: {
    title: 'Spawn',
    publisher: 'Image Comics',
    comicVineId: 1683,
    startYear: 1992,
  },
};

export const testIssues = {
  batman100: {
    issueNumber: 100,
    title: 'The Joker War, Part 1',
    comicVineId: 765432,
    storeDate: '2020-10-06',
  },
  spiderMan1: {
    issueNumber: 1,
    title: 'Back to Basics',
    comicVineId: 654321,
    storeDate: '2018-07-11',
  },
};

export const testUsers = {
  defaultUser: {
    settings: {
      theme: 'dark',
      defaultSeriesView: 'cover',
    },
  },
};

/**
 * API endpoint helper for test setup/teardown.
 */
export class TestDataHelper {
  constructor(private baseUrl: string) {}

  async seedSeries(series: typeof testSeries[keyof typeof testSeries]) {
    const response = await fetch(`${this.baseUrl}/api/v1/series`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(series),
    });
    return response.json();
  }

  async clearAllData() {
    // Note: In a real implementation, this would call a test-only API endpoint
    // that clears the database or resets to a known state
    console.log('Clearing test data...');
  }

  async resetToDefaultState() {
    await this.clearAllData();
    // Seed with minimal default data if needed
  }
}
