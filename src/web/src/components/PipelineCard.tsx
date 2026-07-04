'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePipelineStatus } from '@/hooks/usePipeline';
import { useOperationsQuota } from '@/hooks/useOperations';
import Card from '@/components/ui/Card';
import { Play, CheckCircle, XCircle, AlertCircle, ArrowRight, Loader2 } from 'lucide-react';

export default function PipelineCard() {
  const { data: run, isLoading } = usePipelineStatus();
  const { data: quota, isLoading: isQuotaLoading } = useOperationsQuota();
  const [elapsed, setElapsed] = useState<string>('');

  useEffect(() => {
    if (!run || (run.status !== 'in_progress' && run.status !== 'pending')) {
      const t = setTimeout(() => setElapsed(''), 0);
      return () => clearTimeout(t);
    }

    const interval = setInterval(() => {
      const start = new Date(run.startedAt).getTime();
      const now = new Date().getTime();
      const diffMs = Math.max(0, now - start);

      const hours = Math.floor(diffMs / 3600000);
      const minutes = Math.floor((diffMs % 3600000) / 60000);
      const seconds = Math.floor((diffMs % 60000) / 1000);

      const pad = (num: number) => String(num).padStart(2, '0');
      setElapsed(`${hours > 0 ? hours + ':' : ''}${pad(minutes)}:${pad(seconds)}`);
    }, 1000);

    return () => clearInterval(interval);
  }, [run]);

  if (isLoading) {
    return (
      <Card>
        <div className="flex items-center justify-center py-6">
          <Loader2 className="w-6 h-6 animate-spin text-blue-500" />
          <span className="ml-2 text-sm text-gray-500">Loading background status...</span>
        </div>
      </Card>
    );
  }

  if (!run) {
    return (
      <Card>
        <div className="flex items-center justify-between gap-4">
          <div>
            <h3 className="font-semibold text-gray-900 dark:text-gray-100">Operations Status</h3>
            <p className="text-sm text-gray-500 mt-1">No background tasks have run yet.</p>
            <p className="text-xs text-gray-500 mt-2">
              {isQuotaLoading || !quota
                ? 'Move budget: Checking…'
                : `Move budget: ${quota.movesUsedToday} / ${quota.moveBudget}${quota.isBlocked ? ' blocked' : ''}`}
            </p>
          </div>
          <Link
            href="/operations"
            className="flex items-center gap-1 text-sm font-medium text-blue-600 hover:text-blue-500 dark:text-blue-400"
          >
            Go to Operations <ArrowRight className="w-4 h-4" />
          </Link>
        </div>
      </Card>
    );
  }

  const isActive = run.status === 'in_progress' || run.status === 'pending';

  // Get status color/badge
  const getStatusBadge = (status: string, isStalled?: boolean) => {
    if (isStalled) {
      return (
        <span className="bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300 text-xs font-semibold px-2.5 py-0.5 rounded flex items-center gap-1">
          <span className="w-1.5 h-1.5 bg-red-600 rounded-full animate-ping" />
          Stalled
        </span>
      );
    }
    switch (status) {
      case 'pending':
        return <span className="bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300 text-xs font-semibold px-2.5 py-0.5 rounded">Pending</span>;
      case 'in_progress':
        return (
          <span className="bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300 text-xs font-semibold px-2.5 py-0.5 rounded flex items-center gap-1">
            <span className="w-1.5 h-1.5 bg-blue-600 rounded-full animate-ping" />
            Running
          </span>
        );
      case 'completed':
        return <span className="bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300 text-xs font-semibold px-2.5 py-0.5 rounded">Completed</span>;
      case 'failed':
        return <span className="bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300 text-xs font-semibold px-2.5 py-0.5 rounded">Failed</span>;
      case 'deferred':
        return <span className="bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-300 text-xs font-semibold px-2.5 py-0.5 rounded">Deferred</span>;
      default:
        return <span className="bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-300 text-xs font-semibold px-2.5 py-0.5 rounded">{status}</span>;
    }
  };

  // Build metrics info based on pipeline type
  const isSync = run.pipelineType === 'sync';
  let progressText = '';
  let percentage = 0;

  if (isSync) {
    const total = run.videoMetadataBatchesTotal * 50; // Approximated total videos based on chunk size
    const current = run.videosUpserted;
    if (total > 0) {
      percentage = Math.min(100, Math.round((current / total) * 100));
    }
    progressText = `${run.playlistsProcessed} playlists processed (${current} videos updated)`;
  } else {
    const total = run.videosPendingTagging;
    const current = run.videosProcessed;
    if (total > 0) {
      percentage = Math.min(100, Math.round((current / total) * 100));
    }
    progressText = `${current} / ${total} videos categorized (${run.videosTagged} tags suggested)`;
  }

  return (
    <Card className={
      run.isStalled
        ? 'border-red-200 dark:border-red-900/50 bg-red-50/20 dark:bg-red-950/10 animate-pulse'
        : isActive
        ? 'border-blue-200 dark:border-blue-900/50 bg-blue-50/20 dark:bg-blue-950/10'
        : ''
    }>
      <div className="flex flex-col gap-4">
        {/* Header Row */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-gray-100 dark:bg-gray-800 rounded-lg">
              {run.isStalled ? (
                <AlertCircle className="w-5 h-5 text-red-600 dark:text-red-400" />
              ) : isActive ? (
                <Loader2 className="w-5 h-5 text-blue-600 dark:text-blue-400 animate-spin" />
              ) : run.status === 'completed' ? (
                <CheckCircle className="w-5 h-5 text-green-600 dark:text-green-400" />
              ) : run.status === 'failed' ? (
                <XCircle className="w-5 h-5 text-red-600 dark:text-red-400" />
              ) : (
                <AlertCircle className="w-5 h-5 text-orange-600 dark:text-orange-400" />
              )}
            </div>
            <div>
              <h3 className="font-semibold text-gray-900 dark:text-gray-100 flex items-center gap-2">
                Background {run.pipelineType === 'sync' ? 'Sync' : 'Categorization'}
                {getStatusBadge(run.status, run.isStalled)}
              </h3>
              <p className="text-xs text-gray-500 mt-0.5">
                {run.isStalled ? 'Task Stalled' : isActive ? `Active Phase: ${run.phase}` : `Finished: ${new Date(run.completedAt || run.updatedAt).toLocaleString()}`}
              </p>
            </div>
          </div>
          <Link
            href="/operations"
            className="flex items-center gap-1 text-xs font-semibold text-blue-600 hover:text-blue-500 dark:text-blue-400"
          >
            Detailed Logs <ArrowRight className="w-3.5 h-3.5" />
          </Link>
        </div>

        {/* Status Message / Info */}
        <div className="text-sm">
          {run.isStalled ? (
            <div className="space-y-2">
              <p className="font-medium text-red-700 dark:text-red-400">
                Stalled: No updates received in the last 5 minutes. The background worker may be stuck or offline.
              </p>
              <p className="text-xs text-gray-500">
                Last recorded phase: {run.phase} ({run.currentMessage || 'No status message'})
              </p>
            </div>
          ) : isActive ? (
            <div className="space-y-2">
              <div className="flex justify-between text-xs text-gray-500">
                <span>{run.currentMessage || 'Running pipeline task...'}</span>
                {elapsed && <span className="font-mono bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300 px-1.5 py-0.5 rounded font-bold">{elapsed}</span>}
              </div>
              <div className="w-full bg-gray-200 dark:bg-gray-700 h-2 rounded-full overflow-hidden">
                <div
                  className="bg-blue-600 dark:bg-blue-500 h-full rounded-full transition-all duration-500"
                  style={{ width: `${percentage > 0 ? percentage : 10}%` }}
                />
              </div>
              <div className="flex justify-between text-xs text-gray-500">
                <span>{progressText}</span>
                {percentage > 0 && <span>{percentage}%</span>}
              </div>
            </div>
          ) : (
            <div className="flex flex-col gap-1.5">
              <p className="text-gray-600 dark:text-gray-300">
                {run.status === 'completed'
                  ? `Completed successfully in ${Math.round((new Date(run.completedAt || '').getTime() - new Date(run.startedAt).getTime()) / 1000)}s.`
                  : run.status === 'failed'
                  ? `Failed: ${run.error || 'Unknown error occurred.'}`
                  : `Deferred: ${run.error || 'YouTube API Quota exhausted.'}`}
              </p>
              <div className="text-xs text-gray-500 flex gap-4">
                <span>Videos upserted: {run.videosUpserted}</span>
                <span>Tags suggested: {run.videosTagged}</span>
                {run.errorsCount > 0 && <span className="text-red-500">Errors: {run.errorsCount}</span>}
              </div>
            </div>
          )}
        </div>
        <div className={`rounded-lg border px-3 py-2 text-xs ${
          !quota || isQuotaLoading
            ? 'border-gray-200 bg-gray-50 text-gray-500 dark:border-gray-700 dark:bg-gray-900/40 dark:text-gray-400'
            : quota.isBlocked
            ? 'border-orange-200 bg-orange-50 text-orange-800 dark:border-orange-900/50 dark:bg-orange-950/20 dark:text-orange-300'
            : 'border-blue-200 bg-blue-50 text-blue-800 dark:border-blue-900/50 dark:bg-blue-950/20 dark:text-blue-300'
        }`}>
          {!quota || isQuotaLoading
            ? 'Organize move budget: Checking…'
            : `Organize move budget: ${quota.movesUsedToday} / ${quota.moveBudget} · resets ${new Date(quota.resetsAt).toLocaleTimeString()}`}
        </div>
      </div>
    </Card>
  );
}
