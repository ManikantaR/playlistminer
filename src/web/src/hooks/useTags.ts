import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type { Tag, TagRule } from '@/types';

export function useTags() {
  return useQuery({
    queryKey: ['tags'],
    queryFn: () => apiGet<Tag[]>('/api/tags'),
  });
}

export function useTagRules(tagId: number) {
  return useQuery({
    queryKey: ['tagRules', tagId],
    queryFn: () => apiGet<TagRule[]>(`/api/tags/${tagId}/rules`),
    enabled: tagId > 0,
  });
}
