import { useMutation } from '@tanstack/react-query';
import { apiPost } from '@/lib/api-client';
import type { OrganizeExecutionResult, OrganizePlan } from '@/types';

export function useBuildOrganizePlan() {
  return useMutation({
    mutationFn: () => apiPost<OrganizePlan>('/api/organize/plan'),
  });
}

export function useExecuteOrganize() {
  return useMutation({
    mutationFn: () => apiPost<OrganizeExecutionResult>('/api/organize/execute'),
  });
}
