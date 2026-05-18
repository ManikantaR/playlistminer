---
name: tdd-frontend
description: 'Write a new React component or page using TDD with Jest and React Testing Library for PlaylistMiner Next.js frontend.'
---

# TDD Feature Implementation (Next.js)

Implement the requested component or page using TDD.

## Step 1: Write Jest Test

1. Create test file alongside the component: `ComponentName.test.tsx`
2. Use React Testing Library: `render`, `screen`, `userEvent`
3. Test rendering, user interactions, edge cases
4. Run `npm test` — confirm tests FAIL

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ComponentName } from './ComponentName';

describe('ComponentName', () => {
  it('renders the expected content', () => {
    render(<ComponentName />);
    expect(screen.getByText('Expected')).toBeInTheDocument();
  });
});
```

## Step 2: Implement Component

1. Write minimal component to pass tests
2. TypeScript strict — no `any` types
3. Use TanStack Query for API calls via custom hooks
4. Tailwind CSS only — no custom CSS
5. Run `npm test` — confirm tests PASS

## Step 3: Write E2E Test (if page-level)

1. Add Playwright test in `e2e/` for the user flow
2. Test the happy path end-to-end
3. Run `npm run test:e2e` — confirm it passes

## Rules

- Server components by default, `"use client"` only when needed
- API calls go through hooks in `hooks/`, never directly in components
- All hooks use the auto-generated API client
- Check dark mode and responsive layout
