/**
 * ESLint config for Shortboxerr UI.
 *
 * Accepted warnings (downgraded from error; see BACKLOG 14.23):
 * - react-hooks/set-state-in-effect: Syncing state from URL/API in effects (Layout, pages).
 *   Refactor to derive-in-render where feasible; until then kept as warn.
 * - react-refresh/only-export-components: App.tsx (useTheme), Toast.tsx export hooks + components.
 *   Co-location is intentional; prefer extracting to separate files if adding more exports.
 * - @typescript-eslint/no-explicit-any: api/client.ts uses any in generated/callback types.
 *   Replace with proper types or unknown + guards when touching those call sites.
 * - react-hooks/static-components: Prefer declaring components at module scope (e.g. TriStateCheckbox).
 *   Remaining inline helpers kept as warn until refactor.
 * - Line-level disables (see code): LogsPage TanStack useVirtualizer (incompatible-library);
 *   SeriesDetailPage pagination reset (set-state-in-effect). Iteration 236.
 */
import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    rules: {
      'react-hooks/set-state-in-effect': 'warn',
      'react-refresh/only-export-components': 'warn',
      '@typescript-eslint/no-explicit-any': 'warn',
      'react-hooks/static-components': 'warn',
    },
  },
])
