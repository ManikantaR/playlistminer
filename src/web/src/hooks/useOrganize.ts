import { useMutation } from '@tanstack/react-query';
import { apiPost } from '@/lib/api-client';
import type { AgentProcessResult, OrganizeExecutionResult, OrganizePlan } from '@/types';

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

export function useProcessNow() {
  return useMutation({
    mutationFn: () => apiPost<AgentProcessResult>('/api/agent/process-now'),
  });
}
