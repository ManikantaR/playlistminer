import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPut } from '@/lib/api-client';
import type { AutomationPolicy } from '@/types';

export function useAutomationPolicy() {
  return useQuery({
    queryKey: ['automationPolicy'],
    queryFn: () => apiGet<AutomationPolicy>('/api/automation/policy'),
  });
}

export function useUpdateAutomationPolicy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (policy: AutomationPolicy) =>
      apiPut<AutomationPolicy>('/api/automation/policy', policy),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['automationPolicy'] });
      qc.invalidateQueries({ queryKey: ['operationsQuota'] });
      qc.invalidateQueries({ queryKey: ['pipelineHealth'] });
      qc.invalidateQueries({ queryKey: ['operationsHealth'] });
    },
  });
}
