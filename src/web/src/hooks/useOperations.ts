import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/lib/api-client';
import type { CreateOperationRequest, OperationRequest, OperationsActivityFeed, OperationsQuota } from '@/types';

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

export function useOperationQueue() {
  return useQuery({
    queryKey: ['operationQueue'],
    queryFn: () => apiGet<OperationRequest[]>('/api/operations/queue'),
    refetchInterval: 10000,
  });
}

export function useQueueOperation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateOperationRequest) =>
      apiPost<OperationRequest>('/api/operations/queue', request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['operationQueue'] });
    },
  });
}

export function useCancelOperation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (operationId: number) =>
      apiPost<OperationRequest>(`/api/operations/queue/${operationId}/cancel`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['operationQueue'] });
    },
  });
}
