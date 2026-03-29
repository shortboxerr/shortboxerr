/**
 * ESLint config for Shortboxerr UI.
 *
 * `npm run lint` uses `--max-warnings 0` (zero warnings required in CI).
 *
 * Some rules stay at `warn` so new code gets flagged without failing the whole graph until fixed:
 * - react-hooks/set-state-in-effect, react-refresh/only-export-components,
 *   @typescript-eslint/no-explicit-any, react-hooks/static-components
 *
 * Targeted eslint-disable-next-line is allowed for third-party patterns (e.g. TanStack useVirtualizer).
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
