# Prompt 06: Next.js Frontend

## Context
PlaylistMiner frontend is Next.js 14 + TypeScript + Tailwind CSS. It consumes the C# REST API via auto-generated TypeScript client from the OpenAPI spec. TDD with Jest + React Testing Library for components, Playwright for E2E.

## Prompt 06a: Frontend Project Setup

```
Create a Next.js 14 project in src/web/ with the following setup:

- TypeScript strict mode
- Tailwind CSS with custom theme (dark mode support, toggle)
- App Router (app/ directory)
- ESLint + Prettier configured
- Jest + React Testing Library configured
- Playwright configured (playwright.config.ts)
- Path alias: @/ → src/

Install packages:
- @tanstack/react-query (server state management)
- openapi-typescript-codegen or openapi-fetch (API client generation)
- lucide-react (icons)
- clsx + tailwind-merge (class utilities)
- react-hot-toast (notifications)
- @tanstack/react-table (video list table)
- fuse.js (client-side fuzzy search fallback)

Add npm scripts:
- "dev": next dev
- "build": next build
- "test": jest
- "test:e2e": playwright test
- "generate-api": script to generate TypeScript client from http://localhost:5000/swagger/v1/swagger.json

Create layout structure:
- app/layout.tsx: root layout with sidebar navigation, dark mode toggle
- app/page.tsx: dashboard
- Sidebar links: Dashboard, Videos, Suggestions, Playlists, Tags, Import, Undo, Settings

Create reusable UI components (test each with Jest):
- components/ui/Button.tsx
- components/ui/Input.tsx
- components/ui/Badge.tsx (for tag chips)
- components/ui/Card.tsx
- components/ui/Table.tsx (wrapper around react-table)
- components/ui/SearchInput.tsx (with debounce)
- components/ui/Modal.tsx
- components/ui/EmptyState.tsx
- components/ui/Pagination.tsx
```

## Prompt 06b: Videos Page (TDD)

```
Jest tests first:

VideoList.test.tsx:
- renders_video_list_with_titles
- shows_loading_state
- shows_empty_state_when_no_videos
- filters_by_tag_when_tag_clicked
- search_input_triggers_fuzzy_search
- pagination_loads_next_page
- shows_tag_badges_on_each_video

VideoDetail.test.tsx:
- renders_video_metadata
- renders_current_tags_as_badges
- renders_suggested_tags_differently
- accept_suggestion_calls_api
- reject_suggestion_calls_api
- add_manual_tag_opens_tag_picker
- remove_tag_calls_api

Then implement:

app/videos/page.tsx:
- Search bar at top (debounced, hits GET /api/videos?search=)
- Tag filter chips (multi-select, hits GET /api/videos?tags=)
- Status filter dropdown
- Video list table: thumbnail, title, channel, tags (as colored badges), status, actions
- Pagination controls
- Click row → navigate to video detail

app/videos/[id]/page.tsx:
- Video thumbnail + metadata (title, channel, published date, duration)
- YouTube link (opens in new tab)
- Current tags section: colored badges with X to remove
- Suggested tags section: dashed-border badges with checkmark/X to accept/reject
- "Add Tag" button → opens tag picker modal
- Tag picker: searchable list of all tags with checkboxes

Hooks (in hooks/):
- useVideos(filter): wraps react-query for GET /api/videos
- useVideo(id): wraps react-query for GET /api/videos/{id}
- useUpdateTags(): mutation for PATCH /api/videos/{id}/tags
- useAcceptSuggestion(): mutation for POST accept
- useRejectSuggestion(): mutation for POST reject
```

## Prompt 06c: Suggestions Page (TDD)

```
Jest tests first:

SuggestionQueue.test.tsx:
- renders_videos_with_pending_suggestions
- shows_suggestion_count_in_sidebar_badge
- accept_all_button_accepts_all_suggestions
- shows_confidence_score_on_suggestions
- keyboard_navigation_works (j/k for next/prev, y/n for accept/reject)

Then implement:

app/suggestions/page.tsx:
- List of videos with pending suggestions
- Each video shows: thumbnail, title, suggested tags with confidence %
- Accept/Reject buttons per suggestion
- "Accept All" for high-confidence suggestions
- Keyboard shortcuts: j/k navigate, y accept, n reject
- After action: auto-advance to next video
- Empty state: "All caught up!" message
```

## Prompt 06d: Remaining Pages (TDD)

```
Write Jest tests then implement each page:

app/playlists/page.tsx:
- List of playlists (local + YouTube)
- "Inbox" badge on designated inbox playlist
- Video count per playlist
- "Set as Inbox" action
- "Create Topic Playlist" form
- "Consolidate" button → shows merge preview → confirm

app/tags/page.tsx:
- Tag list with video counts and category grouping
- Create tag form (name + category)
- Expandable row: shows keyword rules for that tag
- Add/remove keyword rules inline
- Delete tag (with confirmation)

app/import/page.tsx:
- Drag-and-drop CSV upload zone
- Upload progress indicator
- Import history table (source, filename, counts, date)

app/undo/page.tsx:
- List of recent move actions within 7-day window
- Shows: video title, from playlist → to playlist, date
- "Undo" button per row
- Expired items shown grayed out

app/settings/page.tsx:
- YouTube connection status (connected/disconnected)
- "Connect YouTube" button → initiates OAuth flow
- Sync schedule display (next run time)
- "Sync Now" button
- Categorization thresholds sliders (keyword threshold, TF-IDF threshold)
- Ollama status (available/unavailable)

app/page.tsx (Dashboard):
- Stats cards: total videos, total tags, pending suggestions, last sync time
- Recent sync history (mini table)
- Quick actions: Sync Now, Review Suggestions
```

## Prompt 06e: Playwright E2E Tests

```
Write E2E tests in src/web/e2e/:

videos.spec.ts:
- can search videos by title
- can filter videos by tag
- can navigate to video detail
- can accept a tag suggestion
- can reject a tag suggestion
- can manually add a tag

import.spec.ts:
- can upload a CSV file
- shows import results
- imported videos appear in video list

sync.spec.ts:
- can trigger manual sync
- sync status updates in real-time
- dashboard shows sync statistics

tags.spec.ts:
- can create a new tag
- can add keyword rule to tag
- can delete a tag with confirmation

search.spec.ts:
- fuzzy search finds partial matches
- tag filter narrows results
- combined search + tag filter works
- empty search shows all videos

Use Playwright fixtures with a test database seeded with sample data.
```

## Verification
- `npm test` passes all Jest tests
- `npm run test:e2e` passes all Playwright tests
- All pages render correctly in browser
- Dark mode toggle works across all pages
- API client is auto-generated from Swagger spec
- Responsive layout works on mobile-width viewport
