# Copilot Instructions — PlaylistMiner Frontend

## Project
Next.js 14 frontend for PlaylistMiner, a YouTube playlist organizer.

## Stack
- Next.js 14 (App Router) + TypeScript (strict) + Tailwind CSS
- TanStack Query for server state
- Auto-generated API client from OpenAPI/Swagger
- Jest + React Testing Library + Playwright

## Code Conventions
- TypeScript strict mode — no `any`, no `@ts-ignore`
- Server components by default — `"use client"` only when needed
- Functional components with hooks
- PascalCase for component files, camelCase for hooks/utils
- Tailwind utility classes only — no custom CSS
- Use `clsx` + `tailwind-merge` for conditional classes

## Component Patterns
```tsx
// Preferred: typed props, destructured, default export
interface VideoCardProps {
  video: VideoDto;
  onTagClick: (tagId: number) => void;
}

export default function VideoCard({ video, onTagClick }: VideoCardProps) {
  return (/* ... */);
}
```

## Hook Patterns
```tsx
// All API hooks wrap react-query
export function useVideos(filter: VideoFilter) {
  return useQuery({
    queryKey: ['videos', filter],
    queryFn: () => api.videos.getAll(filter),
  });
}
```

## Testing
- Jest: test component rendering, user interactions, edge cases
- Playwright: test full user flows (search → click → tag → accept)
- TDD: write tests first, then implement
- Test naming: `describes what it renders/does`

## Do NOT
- Use `any` type
- Skip TypeScript errors with ts-ignore
- Put API calls directly in components (use hooks)
- Use CSS modules or styled-components
- Use class components
- Hardcode API URLs (use environment variables)
