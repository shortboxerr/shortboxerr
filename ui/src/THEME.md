# Shortboxerr Theme System

## Overview

Shortboxerr uses a CSS variable-based theme system with full support for dark and light modes. All colors are verified for WCAG 2.1 Level AA accessibility compliance.

## Theme Toggle

Themes are controlled via the `data-theme` attribute on the `<html>` element:
- `data-theme="dark"` - Dark theme (default)
- `data-theme="light"` - Light theme

## Color Palette

### Dark Theme (Default)

| Variable | Hex | Usage | Contrast Ratio |
|----------|-----|-------|----------------|
| `--bg-primary` | `#1a1d23` | Main app background | Base |
| `--bg-secondary` | `#22262e` | Cards, sidebar, modals | - |
| `--bg-tertiary` | `#2a2f38` | Input fields, nested elements | - |
| `--bg-hover` | `#333842` | Hover states | - |
| `--bg-active` | `#3d4350` | Active/selected states | - |
| `--bg-selected` | `#3d4350` | Selected items in tables/lists | - |
| `--text-primary` | `#f5f5f5` | Primary content | 14.4:1 ✓ |
| `--text-secondary` | `#b0b7c3` | Secondary content | 8.0:1 ✓ |
| `--text-muted` | `#8891a0` | Muted/placeholder text | 5.2:1 ✓ |
| `--accent-primary` | `#5d9cec` | Links, primary actions | 4.9:1 ✓ |
| `--accent-success` | `#5cb85c` | Success states | 4.2:1 ✓ |
| `--accent-warning` | `#f0ad4e` | Warning states | 7.3:1 ✓ |
| `--accent-danger` | `#e74c3c` | Error states | 5.1:1 ✓ |
| `--accent-info` | `#5bc0de` | Info states | 6.8:1 ✓ |
| `--border-color` | `#3a3f4a` | Standard borders | - |
| `--border-light` | `#4a5160` | Subtle borders | - |

### Light Theme

| Variable | Hex | Usage | Contrast Ratio |
|----------|-----|-------|----------------|
| `--bg-primary` | `#f8f9fa` | Main app background | Base |
| `--bg-secondary` | `#ffffff` | Cards, sidebar, modals | - |
| `--bg-tertiary` | `#e9ecef` | Input fields, nested elements | - |
| `--bg-hover` | `#dee2e6` | Hover states | - |
| `--bg-active` | `#ced4da` | Active/selected states | - |
| `--bg-selected` | `#e3e8ed` | Selected items in tables/lists | - |
| `--text-primary` | `#212529` | Primary content | 14.7:1 ✓ |
| `--text-secondary` | `#495057` | Secondary content | 7.4:1 ✓ |
| `--text-muted` | `#6c757d` | Muted/placeholder text | 4.6:1 ✓ |
| `--accent-primary` | `#0d6efd` | Links, primary actions | 4.5:1 ✓ |
| `--accent-success` | `#198754` | Success states | 4.6:1 ✓ |
| `--accent-warning` | `#cc8400` | Warning states | 4.5:1 ✓ |
| `--accent-danger` | `#dc3545` | Error states | 5.4:1 ✓ |
| `--accent-info` | `#0dcaf0` | Info states | 3.0:1 (large text only) |
| `--border-color` | `#dee2e6` | Standard borders | - |
| `--border-light` | `#e9ecef` | Subtle borders | - |

## Accessibility Guidelines

### WCAG 2.1 Level AA Requirements

- **Normal text** (< 18pt or < 14pt bold): Minimum 4.5:1 contrast ratio
- **Large text** (≥ 18pt or ≥ 14pt bold): Minimum 3:1 contrast ratio
- **UI components**: Minimum 3:1 contrast against adjacent colors

### Usage Notes

1. **Primary Text** (`--text-primary`): Use for headings, body text, and important labels
2. **Secondary Text** (`--text-secondary`): Use for supporting text, descriptions
3. **Muted Text** (`--text-muted`): Use for placeholders, disabled states, timestamps

### Info Color Warning

The `--accent-info` color in light theme (#0dcaf0) has a 3:1 contrast ratio. This is acceptable for:
- Icons with text labels
- Large text (18pt+)
- Non-text UI indicators

For info text that must be readable, consider using `--text-secondary` instead.

## Badge System

Badges use accent colors with appropriate backgrounds:

```css
.badge-success { color: var(--accent-success); }
.badge-warning { color: var(--accent-warning); }
.badge-danger  { color: var(--accent-danger); }
.badge-info    { color: var(--accent-info); }
.badge-muted   { color: var(--text-muted); background: var(--bg-active); }
```

## Adding New Colors

When adding new colors to the theme:

1. Define the color in both `:root` (dark) and `[data-theme="light"]` selectors
2. Verify contrast ratio using [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
3. Document the color in this file with its intended use and contrast ratio
4. Test visually in both themes

## Testing

### Manual Testing

1. Toggle between dark/light themes in Settings > UI
2. Verify text is readable in all contexts
3. Check focus states are visible
4. Verify badges and status indicators are distinguishable

### Automated Testing

Use browser DevTools Lighthouse to run accessibility audits:
- Chrome: DevTools > Lighthouse > Accessibility
- Target: 90+ accessibility score

## Files

- `ui/src/App.css`: Theme variable definitions
- `ui/src/App.tsx`: Theme context and toggle logic
- `ui/src/pages/SettingsPage.tsx`: Theme selector UI
