# PlaylistMiner Web — Agent Instructions

## Role
You are working on the Next.js 14 frontend for PlaylistMiner, a YouTube playlist organizer.

## Rules
1. **TDD:** Write Jest component tests before implementing components.
2. **TypeScript strict mode.** No `any` types. No `// @ts-ignore`.
3. **Server components by default.** Only add `"use client"` when interactivity is needed.
4. **Use TanStack Query** for all API calls. No raw fetch in components.
5. **Use the generated API client.** Regenerate with `npm run generate-api` after backend changes.
6. **Tailwind only.** No custom CSS files unless absolutely necessary.
7. **Accessible.** Use semantic HTML, ARIA labels, keyboard navigation.
8. **No inline styles.** Use Tailwind utility classes.

## Tech Stack
- Next.js 14 (App Router), TypeScript strict, Tailwind CSS
- TanStack Query, lucide-react, react-hot-toast, @tanstack/react-table
- Jest + React Testing Library (components), Playwright (E2E)

## File Structure
- `app/` — Pages using App Router
- `components/ui/` — Reusable UI primitives (Button, Badge, Input, Modal, etc.)
- `components/` — Feature-specific components (VideoList, TagPicker, etc.)
- `hooks/` — Custom hooks wrapping react-query (useVideos, useTags, etc.)
- `lib/` — API client, utilities
- `e2e/` — Playwright E2E tests

## When Adding a Page
1. Write Jest tests for the main component (Red)
2. Create the hook wrapping react-query
3. Implement the component (Green)
4. Write Playwright E2E test for the user flow
5. Check dark mode rendering
6. Check responsive layout at mobile width

## API Integration
- API client auto-generated from Swagger spec at `http://localhost:5000/swagger/v1/swagger.json`
- Run `npm run generate-api` to regenerate after backend API changes
- All hooks in `hooks/` wrap the generated client with react-query
