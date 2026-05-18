import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type { UndoLog } from '@/types';

export function useUndoLogs() {
  return useQuery({
    queryKey: ['undoLogs'],
    queryFn: () => apiGet<UndoLog[]>('/api/undo'),
  });
}
