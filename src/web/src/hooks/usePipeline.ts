import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type { PipelineRun, PipelineEvent, DependencyHealth } from '@/types';

export function usePipelineStatus() {
  return useQuery({
    queryKey: ['pipelineStatus'],
    queryFn: async () => {
      try {
        const data = await apiGet<PipelineRun | Record<string, never>>('/api/pipeline/status');
        // Return null if backend returns empty object (indicating no runs have ever started)
        if (data && Object.keys(data).length === 0) return null;
        return data as PipelineRun;
      } catch (err) {
        console.error('Failed to fetch pipeline status:', err);
        return null;
      }
    },
    refetchInterval: (query) => {
      const run = query.state.data;
      if (run && (run.status === 'in_progress' || run.status === 'pending')) {
        return 3000;
      }
      return 10000;
    },
  });
}

export function usePipelineHistory() {
  return useQuery({
    queryKey: ['pipelineHistory'],
    queryFn: () => apiGet<PipelineRun[]>('/api/pipeline/history'),
    refetchInterval: 10000,
  });
}

export function usePipelineRunDetail(runId?: string) {
  return useQuery({
    queryKey: ['pipelineRunDetail', runId],
    queryFn: () => runId ? apiGet<PipelineRun>(`/api/pipeline/history/${runId}`) : Promise.reject('No runId provided'),
    enabled: !!runId,
    refetchInterval: (query) => {
      const run = query.state.data;
      if (run && (run.status === 'in_progress' || run.status === 'pending')) {
        return 3000;
      }
      return false;
    },
  });
}

export function usePipelineEvents(runId?: string) {
  return useQuery({
    queryKey: ['pipelineEvents', runId],
    queryFn: () => runId ? apiGet<PipelineEvent[]>(`/api/pipeline/events?runId=${runId}`) : Promise.resolve([]),
    enabled: !!runId,
    refetchInterval: (query) => {
      const events = query.state.data;
      const isFinished = events?.some(e => e.phase === 'completed' || e.phase === 'failed' || e.phase === 'deferred');
      if (isFinished) return false;
      return 3000;
    },
  });
}

export function usePipelineHealth() {
  return useQuery({
    queryKey: ['pipelineHealth'],
    queryFn: () => apiGet<DependencyHealth>('/api/pipeline/health'),
    refetchInterval: 10000,
  });
}
