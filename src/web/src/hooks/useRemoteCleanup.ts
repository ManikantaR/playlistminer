import { useMutation } from '@tanstack/react-query';
import { apiPost } from '@/lib/api-client';
import type { RemoteDuplicateCleanupItem, RemoteDuplicateCleanupResult } from '@/types';

export function useBuildRemoteCleanupPlan() {
  return useMutation({
    mutationFn: () => apiPost<RemoteDuplicateCleanupItem[]>('/api/operations/duplicates/plan-remote-cleanup'),
  });
}

export function useExecuteRemoteCleanup() {
  return useMutation({
    mutationFn: (plan: RemoteDuplicateCleanupItem[]) =>
      apiPost<RemoteDuplicateCleanupResult>('/api/operations/duplicates/execute-remote-cleanup', plan),
  });
}
