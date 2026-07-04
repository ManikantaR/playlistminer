import { useMutation } from '@tanstack/react-query';
import { apiPost } from '@/lib/api-client';
import type { OrganizePlan } from '@/types';

export function useBuildOrganizePlan() {
  return useMutation({
    mutationFn: () => apiPost<OrganizePlan>('/api/organize/plan'),
  });
}
