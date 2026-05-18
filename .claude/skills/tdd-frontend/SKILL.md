---
name: tdd-frontend
description: Implement a Next.js component or page using TDD with Jest, React Testing Library, and Playwright for PlaylistMiner frontend.
---

# TDD Feature Implementation (Next.js)

Implement the requested component or page using TDD.

## Step 1: Write Jest Test

1. Create test file alongside the component: `ComponentName.test.tsx`
2. Use React Testing Library: `render`, `screen`, `userEvent`
3. Test rendering, user interactions, edge cases
4. Run `npm test` in `src/web/` — confirm tests FAIL

## Step 2: Implement Component

1. Write minimal component to pass tests
2. TypeScript strict — no `any` types
3. Use TanStack Query for API calls via custom hooks in `hooks/`
4. Tailwind CSS only — no custom CSS
5. Server components by default, `"use client"` only for interactivity
6. Run `npm test` — confirm tests PASS

## Step 3: E2E Test (for pages)

1. Add Playwright test in `src/web/e2e/` for the user flow
2. Test the happy path end-to-end
3. Run `npm run test:e2e`

## Rules

- API calls go through hooks, never directly in components
- All hooks use the auto-generated OpenAPI client
- Check dark mode rendering
- Check responsive layout at mobile width
