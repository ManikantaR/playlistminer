import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type { DuplicateReview } from '@/types';

export function useDuplicateReview() {
  return useQuery({
    queryKey: ['duplicateReview'],
    queryFn: () => apiGet<DuplicateReview[]>('/api/operations/duplicates'),
    refetchInterval: 10000,
  });
}
