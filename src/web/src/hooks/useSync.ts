import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type { SyncLog } from '@/types';

export function useSyncHistory() {
  return useQuery({
    queryKey: ['syncHistory'],
    queryFn: () => apiGet<SyncLog[]>('/api/sync/history'),
  });
}

export function useSyncStatus() {
  return useQuery({
    queryKey: ['syncStatus'],
    queryFn: () => apiGet<{ isRunning: boolean; lastSync: string | null }>('/api/sync/status'),
    refetchInterval: 5000,
  });
}
