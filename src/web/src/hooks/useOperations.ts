import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type { OperationsActivityFeed, OperationsQuota } from '@/types';

export function useOperationsActivity(limit = 10, offset = 0) {
  return useQuery({
    queryKey: ['operationsActivity', limit, offset],
    queryFn: () => apiGet<OperationsActivityFeed>(`/api/operations/activity?limit=${limit}&offset=${offset}`),
    refetchInterval: 10000,
  });
}

export function useOperationsQuota() {
  return useQuery({
    queryKey: ['operationsQuota'],
    queryFn: () => apiGet<OperationsQuota>('/api/operations/quota'),
    refetchInterval: 10000,
  });
}
