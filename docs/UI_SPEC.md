# UI Spec (Arr-like)

## Technology Stack

- **Framework**: React 18 with TypeScript
- **Build Tool**: Vite
- **State Management**: React Query (@tanstack/react-query)
- **Routing**: React Router v6
- **Icons**: Lucide React
- **Styling**: CSS with CSS Variables (no framework)

## Development

```bash
# Install dependencies
cd ui
npm install

# Start dev server (with API proxy to :8585)
npm run dev

# Build for production (outputs to src/Shortboxerr.Api/wwwroot)
npm run build
```

Dev server runs on `http://localhost:3000` and proxies `/api/*` to the backend.

## Navigation

The sidebar contains these sections:

### Main
- **Dashboard** (`/`) - Stats cards, system status, recent activity
- **Series** (`/series`) - Table with search, bulk actions
- **Collections** (`/collections`) - TPBs, hardcovers, omnibuses
- **Wanted** (`/wanted`) - Missing issues and collections

### Activity
- **Activity** (`/activity`) - Download queue with progress
- **History** (`/history`) - Event log with filtering
- **Manual Import** (`/import`) - Staged file review

### System
- **Settings** (`/settings`) - Tabbed configuration

## Pages

### Dashboard
- **Stats Cards**: Series count, collections count, issues count, files count
- **System Status**: Database health, indexer status, queue count
- **Recent Activity**: Last 5 events from history

### Series Page
- **Toolbar**: Search input, refresh button
- **Table**: Checkbox, Title, Year, Publisher, Status, Issues, Files, Actions
- **Bulk Actions**: Delete selected (appears when items selected)
- **Status Badges**: Continuing (green), Ended (gray), Hiatus (yellow)

### Collections Page
- **Toolbar**: Search input, refresh button
- **Table**: Checkbox, Title, Series, Type, Volume, Year, Status, Actions
- **Type Badges**: TPB (blue), HC (green), Omnibus (yellow), Deluxe (blue)
- **Status Badges**: Have (green), Missing (yellow)

### Wanted Page
- **Tab Selector**: Issues / Collections toggle
- **Table**: Title, Series, Issue/Type, Volume (for collections), Added date, Actions
- **Actions**: Search for download, Manual download

### Activity Page
- **Queue List**: Cards showing download progress
- **Card Info**: Title, series, provider, progress bar, time remaining
- **Controls**: Pause/Resume, Remove

### History Page
- **Filter Dropdown**: All Events, Grabbed, Imported, Deleted, Failed, Renamed
- **Search Input**: Filter by title
- **Event List**: Cards with icon, title, series, details, timestamp
- **Event Types**: Color-coded icons (success/info/warning/danger)

### Manual Import Page
- **Stats Row**: Total files, auto-matched, needs review
- **Table**: File, Parsed Info, Match arrow, Match result, Status, Actions
- **Actions**: Edit match, Reject
- **Bulk Import**: Select matched files and import

### Settings Page
- **Tab Navigation**: General, Indexers, Download Clients, Import, UI, Security
- **Form Fields**: Text inputs, selects, checkboxes
- **Browse Buttons**: For folder selection
- **Save Button**: In header

## Patterns

### Tables
- Full-width with rounded container
- Header row with uppercase labels
- Row hover highlight
- Checkbox column for selection
- Actions column aligned right

### Modals
- Overlay with centered card
- Header with title and close button
- Body with form content
- Footer with action buttons

### Empty States
- Centered layout with icon
- Title and description
- Optional action button

### Loading States
- Spinner animation
- Centered in container

### Badges
- Pill-shaped labels
- Color variants: success, warning, danger, info, muted
- Uppercase text

## Theme

### Colors (Dark Mode)
```css
--bg-primary: #1a1d23      /* Page background */
--bg-secondary: #22262e    /* Cards, sidebar */
--bg-tertiary: #2a2f38     /* Inputs, table headers */
--bg-hover: #333842        /* Hover states */
--bg-active: #3d4350       /* Active states */

--text-primary: #f5f5f5    /* Headings, emphasis */
--text-secondary: #9ba1ab  /* Body text */
--text-muted: #6c7380      /* Labels, hints */

--accent-primary: #5d9cec  /* Links, primary buttons */
--accent-success: #5cb85c  /* Success badges */
--accent-warning: #f0ad4e  /* Warning badges */
--accent-danger: #d9534f   /* Error badges, delete */
--accent-info: #5bc0de     /* Info badges */

--border-color: #3a3f4a    /* Borders, dividers */
```

### Typography
- **Font Family**: Inter (via Google Fonts)
- **Monospace**: JetBrains Mono (for API keys, paths)
- **Base Size**: 14px
- **Line Height**: 1.5

### Spacing
- **Sidebar Width**: 220px
- **Page Padding**: 24px
- **Card Padding**: 20px
- **Table Cell Padding**: 12px 16px

### Animations
- Hover transitions: 150ms ease
- Spinner: 800ms linear infinite
- Progress bar: 300ms ease

## API Client

The UI uses a typed API client (`src/api/client.ts`) that:
- Wraps fetch with JSON headers
- Handles errors gracefully (returns defaults for dashboard)
- Formats dates as relative times
- Maps backend types to UI types

## Building for Production

The Vite build outputs to `src/Shortboxerr.Api/wwwroot`:
- `index.html` - Entry point
- `assets/index-*.js` - Bundled JavaScript
- `assets/index-*.css` - Bundled CSS

The ASP.NET API serves these files via:
- `UseDefaultFiles()` - Serves index.html by default
- `UseStaticFiles()` - Serves assets folder
- `MapFallbackToFile("index.html")` - SPA routing fallback
