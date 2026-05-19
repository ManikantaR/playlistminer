import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';

interface QuotaStatus {
  isExhausted: boolean;
  exhaustedAt: string | null;
  resetsAt: string;
  message: string;
}

export function useQuotaStatus() {
  return useQuery({
    queryKey: ['quotaStatus'],
    queryFn: () => apiGet<QuotaStatus>('/api/quota/status'),
    refetchInterval: 60_000,
  });
}
