'use client';

import { useState, useEffect } from 'react';
import {
  usePipelineStatus,
  usePipelineHistory,
  usePipelineEvents,
  usePipelineHealth,
  useOperationsHealth,
  useReclassifyGeneratedTags
} from '@/hooks/usePipeline';
import { useDuplicateReview } from '@/hooks/useDuplicates';
import { useBuildRemoteCleanupPlan, useExecuteRemoteCleanup } from '@/hooks/useRemoteCleanup';
import { useOperationsActivity, useOperationsQuota } from '@/hooks/useOperations';
import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Modal from '@/components/ui/Modal';
import Input from '@/components/ui/Input';
import {
  Activity,
  Play,
  CheckCircle,
  XCircle,
  AlertTriangle,
  AlertCircle,
  Database,
  Key,
  ShieldCheck,
  Cpu,
  RefreshCw,
  Clock,
  History,
  Terminal
} from 'lucide-react';
import type { RemoteDuplicateCleanupItem } from '@/types';

const MAX_REMOTE_CLEANUP_REMOVALS_PER_RUN = 25;
const DEFAULT_REMOTE_CLEANUP_BATCH_SIZE = 5;

// Renders a dependency-health pill. Critically, when `loaded` is false (the health fetch
// hasn't succeeded yet — e.g. first load during an API restart) it shows a neutral
// "Checking…" rather than falsely flashing every dependency as down.
function HealthPill({
  loaded,
  ok,
  okLabel,
  badLabel,
  warn = false,
}: {
  loaded: boolean;
  ok: boolean;
  okLabel: string;
  badLabel: string;
  warn?: boolean;
}) {
  const cls = !loaded
    ? 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-400'
    : ok
    ? 'bg-green-100 text-green-800 dark:bg-green-900/20 dark:text-green-300'
    : warn
    ? 'bg-orange-100 text-orange-800 dark:bg-orange-900/20 dark:text-orange-300'
    : 'bg-red-100 text-red-800 dark:bg-red-900/20 dark:text-red-300';
  return (
    <span className={`px-2 py-0.5 rounded text-xs font-semibold ${cls}`}>
      {!loaded ? 'Checking…' : ok ? okLabel : badLabel}
    </span>
  );
}

function getPipelineDisplayName(pipelineType: string) {
  switch (pipelineType) {
    case 'sync':
      return 'Sync Job';
    case 'remote-duplicate-cleanup':
      return 'Remote Cleanup';
    case 'organize-execute':
      return 'Organize Execute';
    case 'reclassification':
      return 'Reclassification';
    default:
      return 'Categorization Job';
  }
}

function isManualInterventionFailure(run: { pipelineType: string; status: string; error: string | null }) {
  return run.pipelineType === 'organize-execute'
    && run.status === 'failed'
    && !!run.error
    && run.error.includes('Manual cleanup is required');
}

export default function OperationsPage() {
  const { data: activeRun, refetch: refetchStatus } = usePipelineStatus();
  const { data: history, refetch: refetchHistory } = usePipelineHistory();
  const { data: health, refetch: refetchHealth } = usePipelineHealth();
  const { data: opsHealth, refetch: refetchOpsHealth } = useOperationsHealth();
  const reclassifyGeneratedTags = useReclassifyGeneratedTags();
  const [activityOffset, setActivityOffset] = useState(0);
  const activityLimit = 10;
  const { data: activityFeed, refetch: refetchActivity } = useOperationsActivity(activityLimit, activityOffset);
  const { data: operationsQuota, isLoading: isOperationsQuotaLoading, refetch: refetchOperationsQuota } = useOperationsQuota();
  const { data: duplicates } = useDuplicateReview();
  const remoteCleanupPlan = useBuildRemoteCleanupPlan();
  const remoteCleanupExecution = useExecuteRemoteCleanup();
  const [confirmRemoteCleanupOpen, setConfirmRemoteCleanupOpen] = useState(false);
  const [confirmReclassifyOpen, setConfirmReclassifyOpen] = useState(false);
  const [remoteCleanupBatchSize, setRemoteCleanupBatchSize] = useState(DEFAULT_REMOTE_CLEANUP_BATCH_SIZE);

  // If there's an active run, we fetch events for it. Otherwise, we fetch events for the latest run.
  const selectedRunId = activeRun?.runId;
  const { data: events, refetch: refetchEvents } = usePipelineEvents(selectedRunId);

  const [elapsed, setElapsed] = useState<string>('');

  // Calculate elapsed time for active run
  useEffect(() => {
    if (!activeRun || (activeRun.status !== 'in_progress' && activeRun.status !== 'pending')) {
      const t = setTimeout(() => setElapsed(''), 0);
      return () => clearTimeout(t);
    }

    const updateTimer = () => {
      const start = new Date(activeRun.startedAt).getTime();
      const now = new Date().getTime();
      const diffMs = Math.max(0, now - start);

      const hours = Math.floor(diffMs / 3600000);
      const minutes = Math.floor((diffMs % 3600000) / 60000);
      const seconds = Math.floor((diffMs % 60000) / 1000);

      const pad = (num: number) => String(num).padStart(2, '0');
      setElapsed(`${hours > 0 ? hours + ':' : ''}${pad(minutes)}:${pad(seconds)}`);
    };

    updateTimer();
    const interval = setInterval(updateTimer, 1000);
    return () => clearInterval(interval);
  }, [activeRun]);

  const handleManualRefresh = () => {
    refetchStatus();
    refetchHistory();
    refetchHealth();
    refetchOpsHealth();
    refetchActivity();
    refetchOperationsQuota();
    if (selectedRunId) refetchEvents();
  };

  const buildRemoteCleanupPlan = async () => {
    await remoteCleanupPlan.mutateAsync();
  };

  const countRemoteCleanupRemovals = (plan: RemoteDuplicateCleanupItem[]) =>
    plan.reduce((total, item) => total + item.loserPlaylists.length, 0);

  const limitRemoteCleanupPlan = (plan: RemoteDuplicateCleanupItem[], maxRemovals: number) => {
    let remaining = maxRemovals;

    return plan.reduce<RemoteDuplicateCleanupItem[]>((limited, item) => {
      if (remaining <= 0) {
        return limited;
      }

      const loserPlaylists = item.loserPlaylists.slice(0, remaining);
      if (loserPlaylists.length === 0) {
        return limited;
      }

      limited.push({
        ...item,
        loserPlaylists,
        hasUnresolvedRemovals: loserPlaylists.some(playlist => !playlist.playlistItemId),
      });

      remaining -= loserPlaylists.length;
      return limited;
    }, []);
  };

  const executeRemoteCleanup = async () => {
    if (!remoteCleanupPlan.data || remoteCleanupPlan.data.length === 0) return;
    const limitedPlan = limitRemoteCleanupPlan(remoteCleanupPlan.data, effectiveRemoteCleanupBatchSize);
    if (limitedPlan.length === 0) return;
    setConfirmRemoteCleanupOpen(false);
    await remoteCleanupExecution.mutateAsync(limitedPlan);
  };

  const executeReclassify = async () => {
    setConfirmReclassifyOpen(false);
    await reclassifyGeneratedTags.mutateAsync();
    handleManualRefresh();
  };

  const hasUnresolvedRemoteCleanupItems = !!remoteCleanupPlan.data?.some(item => item.hasUnresolvedRemovals);
  const totalRemoteCleanupRemovals = remoteCleanupPlan.data ? countRemoteCleanupRemovals(remoteCleanupPlan.data) : 0;
  const effectiveRemoteCleanupBatchSize = Math.max(
    1,
    Math.min(remoteCleanupBatchSize, totalRemoteCleanupRemovals || 1, MAX_REMOTE_CLEANUP_REMOVALS_PER_RUN),
  );

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'pending':
        return <span className="bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300 text-xs font-semibold px-2.5 py-1 rounded-full">Pending</span>;
      case 'in_progress':
        return (
          <span className="bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300 text-xs font-semibold px-2.5 py-1 rounded-full flex items-center gap-1">
            <span className="w-1.5 h-1.5 bg-blue-600 rounded-full animate-ping" />
            Running
          </span>
        );
      case 'completed':
        return <span className="bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300 text-xs font-semibold px-2.5 py-1 rounded-full">Completed</span>;
      case 'failed':
        return <span className="bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300 text-xs font-semibold px-2.5 py-1 rounded-full">Failed</span>;
      case 'deferred':
        return <span className="bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-300 text-xs font-semibold px-2.5 py-1 rounded-full">Deferred</span>;
      default:
        return <span className="bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-300 text-xs font-semibold px-2.5 py-1 rounded-full">{status}</span>;
    }
  };

  // Determine elapsed time of a finished run
  const getDurationString = (run: any) => {
    if (!run.completedAt) return '–';
    const start = new Date(run.startedAt).getTime();
    const end = new Date(run.completedAt).getTime();
    const diffSeconds = Math.max(0, Math.round((end - start) / 1000));

    if (diffSeconds < 60) return `${diffSeconds}s`;
    const minutes = Math.floor(diffSeconds / 60);
    const seconds = diffSeconds % 60;
    return `${minutes}m ${seconds}s`;
  };

  // Format historical run counters summary
  const getCountersSummary = (run: any) => {
    if (run.pipelineType === 'sync') {
      return `Upserted: ${run.videosUpserted} • Playlists: ${run.playlistsProcessed} • Links: ${run.playlistVideoLinksWritten}`;
    }
    if (run.pipelineType === 'remote-duplicate-cleanup') {
      return `Executed: ${run.videosProcessed} • Skipped: ${run.videosSkipped} • Deferred: ${run.videosDeferred}`;
    }
    return `Processed: ${run.videosProcessed} • Tagged: ${run.videosTagged} • Skipped: ${run.videosSkipped}`;
  };

  const getBannerState = () => {
    // Until the health snapshot loads, stay neutral rather than prematurely flashing green.
    if (!opsHealth && !activeRun) {
      return {
        title: 'Checking System Status…',
        message: 'Fetching the latest dependency health and background-task state.',
        bg: 'bg-gray-100 text-gray-800 border-gray-300 dark:bg-gray-900/50 dark:text-gray-200 dark:border-gray-800',
        icon: <RefreshCw className="w-5 h-5 text-gray-500 animate-spin" />
      };
    }
    if (opsHealth) {
      if (!opsHealth.dbHealthy || !opsHealth.workerHealthy) {
        return {
          title: 'Dependency Unavailable',
          message: 'The background worker process or database is offline. Please check your hosting deployment.',
          bg: 'bg-gray-100 text-gray-800 border-gray-300 dark:bg-gray-900/50 dark:text-gray-200 dark:border-gray-800',
          icon: <AlertTriangle className="w-5 h-5 text-gray-500 animate-pulse" />
        };
      }
      if (opsHealth.quotaExhausted) {
        return {
          title: 'YouTube API Quota Blocked',
          message: 'Daily YouTube API quota has been exhausted. Background processes are paused and will defer until tomorrow.',
          bg: 'bg-orange-50 text-orange-800 border-orange-200 dark:bg-orange-950/20 dark:text-orange-300 dark:border-orange-900/50',
          icon: <AlertTriangle className="w-5 h-5 text-orange-500" />
        };
      }
      if (opsHealth.activeRunStalled) {
        return {
          title: 'System Stalled',
          message: `The active run in phase "${opsHealth.activeRunPhase || 'unknown'}" has stalled (no updates written recently).`,
          bg: 'bg-red-50 text-red-800 border-red-200 dark:bg-red-950/20 dark:text-red-300 dark:border-red-900/50 animate-pulse',
          icon: <AlertCircle className="w-5 h-5 text-red-500 animate-bounce" />
        };
      }
    }

    if (activeRun) {
      if (isManualInterventionFailure(activeRun)) {
        return {
          title: 'Manual Cleanup Required',
          message: 'The last organize execution hit an irrecoverable remote partial failure. YouTube state may now be inconsistent and remaining organize moves have been deferred.',
          bg: 'bg-red-50 text-red-800 border-red-200 dark:bg-red-950/20 dark:text-red-300 dark:border-red-900/50',
          icon: <AlertTriangle className="w-5 h-5 text-red-500" />
        };
      }
      if (activeRun.status === 'failed') {
        return {
          title: 'Pipeline Run Failed',
          message: `The last background operation failed: "${activeRun.error || 'Unknown error'}".`,
          bg: 'bg-red-50 text-red-800 border-red-200 dark:bg-red-950/20 dark:text-red-300 dark:border-red-900/50',
          icon: <XCircle className="w-5 h-5 text-red-500" />
        };
      }
      if (activeRun.status === 'in_progress' || activeRun.status === 'pending') {
        return {
          title: 'System Processing',
          message: `Currently executing: ${getPipelineDisplayName(activeRun.pipelineType)} (Phase: ${activeRun.phase}).`,
          bg: 'bg-blue-50 text-blue-800 border-blue-200 dark:bg-blue-950/20 dark:text-blue-300 dark:border-blue-900/50',
          icon: <Activity className="w-5 h-5 text-blue-500 animate-spin" />
        };
      }
    }

    return {
      title: 'System Healthy & Idle',
      message: 'All core dependencies are healthy and background tasks are ready.',
      bg: 'bg-green-50 text-green-800 border-green-200 dark:bg-green-950/20 dark:text-green-300 dark:border-green-900/50',
      icon: <CheckCircle className="w-5 h-5 text-green-500" />
    };
  };

  const banner = getBannerState();
  const quotaResetDistance = operationsQuota
    ? getResetDistance(operationsQuota.resetsAt)
    : null;
  const activityItems = activityFeed?.items ?? [];

  return (
    <div className="max-w-6xl mx-auto space-y-6">
      {/* Title Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Activity className="w-6 h-6 text-blue-600 dark:text-blue-400" />
          <h1 className="text-2xl font-bold">System Operations</h1>
        </div>
        <div className="flex items-center gap-2">
          <Button
            onClick={() => setConfirmReclassifyOpen(true)}
            variant="secondary"
            disabled={reclassifyGeneratedTags.isPending || activeRun?.status === 'in_progress'}
          >
            <Database className="w-4 h-4 mr-2" />
            Reclassify Tags
          </Button>
          <Button onClick={handleManualRefresh} variant="secondary">
            <RefreshCw className="w-4 h-4 mr-2" />
            Refresh
          </Button>
        </div>
      </div>

      {/* Operations Status Banner */}
      <div className={`p-4 border rounded-lg flex items-start gap-3 ${banner.bg}`}>
        <div className="mt-0.5 shrink-0">{banner.icon}</div>
        <div>
          <h4 className="font-semibold text-sm capitalize">{banner.title}</h4>
          <p className="text-xs mt-1 text-gray-600 dark:text-gray-400">{banner.message}</p>
        </div>
      </div>

      {/* Top Grid: Health Status & Active Task */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Dependency Health Monitor */}
        <Card className="lg:col-span-1">
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <Database className="w-5 h-5 text-gray-500" />
            Dependency Health
          </h2>
          <div className="space-y-4">
            {/* DB Health */}
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium text-gray-600 dark:text-gray-400 flex items-center gap-2">
                <Database className="w-4 h-4" /> Database
              </span>
              <HealthPill loaded={!!health} ok={health?.database === 'healthy'} okLabel="Healthy" badLabel="Unhealthy" />
            </div>

            {/* OAuth Connection */}
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium text-gray-600 dark:text-gray-400 flex items-center gap-2">
                <Key className="w-4 h-4" /> YouTube OAuth
              </span>
              <HealthPill loaded={!!health} ok={!!health?.oauthConnected} okLabel="Connected" badLabel="Disconnected" />
            </div>

            {/* Quota Status */}
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium text-gray-600 dark:text-gray-400 flex items-center gap-2">
                <ShieldCheck className="w-4 h-4" /> YouTube Quota
              </span>
              <HealthPill loaded={!!health} ok={!!health?.youtubeQuotaAvailable} okLabel="Available" badLabel="Exhausted" warn />
            </div>

            {/* Worker Heartbeat */}
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium text-gray-600 dark:text-gray-400 flex items-center gap-2">
                <Clock className="w-4 h-4" /> Worker Engine
              </span>
              <span
                className={`px-2 py-0.5 rounded text-xs font-semibold ${
                  !health
                    ? 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-400'
                    : health.workerStatus === 'healthy'
                    ? 'bg-green-100 text-green-800 dark:bg-green-900/20 dark:text-green-300'
                    : health.workerStatus === 'stale'
                    ? 'bg-orange-100 text-orange-800 dark:bg-orange-900/20 dark:text-orange-300'
                    : 'bg-gray-100 text-gray-800 dark:bg-gray-900/20 dark:text-gray-300'
                }`}
              >
                {!health
                  ? 'Checking…'
                  : health.workerStatus === 'healthy'
                  ? 'Healthy'
                  : health.workerStatus === 'stale'
                  ? 'Stale (Inactive)'
                  : 'Offline / Unknown'}
              </span>
            </div>

            {/* Ollama Status */}
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium text-gray-600 dark:text-gray-400 flex items-center gap-2">
                <Cpu className="w-4 h-4" /> Ollama AI Engine
              </span>
              <span
                className={`px-2 py-0.5 rounded text-xs font-semibold ${
                  !health
                    ? 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-400'
                    : health.ollamaReachable
                    ? 'bg-green-100 text-green-800 dark:bg-green-900/20 dark:text-green-300'
                    : 'bg-gray-100 text-gray-800 dark:bg-gray-900/20 dark:text-gray-400'
                }`}
              >
                {!health ? 'Checking…' : health.ollamaReachable ? 'Online' : 'Offline (Fallback to rules/TF-IDF)'}
              </span>
            </div>
          </div>
        </Card>

        {/* Current / Active Run Status Card */}
        <Card className="lg:col-span-2">
          <h2 className="text-lg font-semibold mb-4 flex items-center justify-between">
            <span className="flex items-center gap-2">
              <Activity className="w-5 h-5 text-gray-500" />
              {activeRun && (activeRun.status === 'in_progress' || activeRun.status === 'pending') ? 'Active Run Progress' : 'Most Recent Execution'}
            </span>
            {activeRun && getStatusBadge(activeRun.status)}
          </h2>

          {!activeRun ? (
            <div className="flex flex-col items-center justify-center py-10 text-center">
              <History className="w-12 h-12 text-gray-300 dark:text-gray-600 mb-2" />
              <p className="text-sm text-gray-500 font-medium">No background runs have executed yet.</p>
            </div>
          ) : (
            <div className="space-y-4">
              {/* Status Header Details */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-xs bg-gray-50 dark:bg-gray-800/40 p-3 rounded-lg border dark:border-gray-800">
                <div>
                  <p className="text-gray-500">Pipeline Type</p>
                  <p className="font-semibold text-gray-800 dark:text-gray-200 capitalize">{activeRun.pipelineType}</p>
                </div>
                <div>
                  <p className="text-gray-500">Current Phase</p>
                  <p className="font-semibold text-gray-800 dark:text-gray-200 capitalize">{activeRun.phase}</p>
                </div>
                <div>
                  <p className="text-gray-500">Started At</p>
                  <p className="font-semibold text-gray-800 dark:text-gray-200">{new Date(activeRun.startedAt).toLocaleTimeString()}</p>
                </div>
                <div>
                  <p className="text-gray-500">{activeRun.completedAt ? 'Duration' : 'Elapsed Time'}</p>
                  <p className="font-semibold text-blue-600 dark:text-blue-400 font-mono text-sm">
                    {activeRun.completedAt ? getDurationString(activeRun) : (elapsed || '–')}
                  </p>
                </div>
              </div>

              {/* Message Banner */}
              <div className="text-sm border-l-4 border-blue-500 pl-3 py-1">
                <p className="font-medium text-gray-700 dark:text-gray-300">{activeRun.currentMessage || 'Running tasks...'}</p>
              </div>

              {/* Error banner if failed */}
              {activeRun.status === 'failed' && activeRun.error && (
                <div className="bg-red-50 dark:bg-red-950/20 border-l-4 border-red-500 p-3 rounded text-sm text-red-800 dark:text-red-300">
                  <p className="font-semibold">Execution Failure Details</p>
                  <p className="mt-1 font-mono text-xs overflow-x-auto whitespace-pre-wrap">{activeRun.error}</p>
                </div>
              )}

              {isManualInterventionFailure(activeRun) && (
                <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-900 dark:border-red-900/40 dark:bg-red-950/20 dark:text-red-200">
                  <p className="font-semibold">Operator Action Required</p>
                  <p className="mt-2">
                    Inspect the affected playlists on YouTube, reconcile the duplicate or half-moved video manually, and only then retry organize execution.
                  </p>
                  <p className="mt-2">
                    Remaining organize moves have been deferred to prevent compounding the inconsistency.
                  </p>
                </div>
              )}

              {/* Progress bars & details if active */}
              {(activeRun.status === 'in_progress' || activeRun.status === 'pending') && (
                <div className="space-y-1">
                  <div className="flex justify-between text-xs text-gray-500">
                    <span>Task Progress</span>
                    <span>
                      {activeRun.pipelineType === 'sync'
                        ? `${activeRun.playlistsProcessed} playlists completed`
                        : activeRun.pipelineType === 'remote-duplicate-cleanup'
                        ? `${activeRun.videosProcessed} removals executed`
                        : `${activeRun.videosProcessed} / ${activeRun.videosPendingTagging} videos tagged`}
                    </span>
                  </div>
                  <div className="w-full bg-gray-200 dark:bg-gray-700 h-2 rounded-full overflow-hidden">
                    <div
                      className="bg-blue-600 dark:bg-blue-500 h-full rounded-full transition-all duration-500"
                      style={{
                        width: `${
                          activeRun.pipelineType === 'sync'
                            ? activeRun.videoMetadataBatchesTotal > 0
                              ? Math.min(100, Math.round((activeRun.videosUpserted / (activeRun.videoMetadataBatchesTotal * 50)) * 100))
                              : 10
                            : activeRun.pipelineType === 'remote-duplicate-cleanup'
                            ? 10
                            : activeRun.videosPendingTagging > 0
                            ? Math.min(100, Math.round((activeRun.videosProcessed / activeRun.videosPendingTagging) * 100))
                            : 10
                        }%`
                      }}
                    />
                  </div>
                </div>
              )}
            </div>
          )}
        </Card>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        <Card className="xl:col-span-1">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="text-lg font-semibold">Organize Move Budget</h2>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Tracks today&apos;s YouTube mutation budget across organize-style actions.
              </p>
            </div>
            <span className={`rounded-full px-3 py-1 text-xs font-semibold ${
              !operationsQuota || isOperationsQuotaLoading
                ? 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-400'
                : operationsQuota.isBlocked
                ? 'bg-orange-100 text-orange-800 dark:bg-orange-950/30 dark:text-orange-300'
                : 'bg-blue-100 text-blue-800 dark:bg-blue-950/30 dark:text-blue-300'
            }`}>
              {!operationsQuota || isOperationsQuotaLoading
                ? 'Checking…'
                : operationsQuota.isBlocked
                ? 'Blocked'
                : 'Available'}
            </span>
          </div>

          {!operationsQuota || isOperationsQuotaLoading ? (
            <div className="mt-4 rounded-lg border border-dashed border-gray-300 p-4 text-sm text-gray-500 dark:border-gray-700 dark:text-gray-400">
              Checking move budget…
            </div>
          ) : (
            <div className="mt-4 space-y-4">
              <div className={`rounded-lg border p-4 ${
                operationsQuota.isBlocked
                  ? 'border-orange-200 bg-orange-50 dark:border-orange-900/50 dark:bg-orange-950/20'
                  : 'border-blue-200 bg-blue-50 dark:border-blue-900/50 dark:bg-blue-950/20'
              }`}>
                <p className="text-sm font-semibold text-gray-900 dark:text-gray-100">
                  {`moves: ${operationsQuota.movesUsedToday} / ${operationsQuota.moveBudget}`}
                </p>
                <p className="mt-1 text-xs text-gray-600 dark:text-gray-300">
                  {operationsQuota.isBlocked
                    ? `Move budget blocked until reset in ${quotaResetDistance}.`
                    : `Resets in ${quotaResetDistance}.`}
                </p>
              </div>
              <div className="grid grid-cols-2 gap-3 text-sm">
                <div className="rounded-lg bg-gray-50 p-3 dark:bg-gray-800/60">
                  <p className="text-xs text-gray-500">Moves Used</p>
                  <p className="mt-1 text-xl font-bold text-gray-900 dark:text-gray-100">{operationsQuota.movesUsedToday}</p>
                </div>
                <div className="rounded-lg bg-gray-50 p-3 dark:bg-gray-800/60">
                  <p className="text-xs text-gray-500">Remaining</p>
                  <p className="mt-1 text-xl font-bold text-gray-900 dark:text-gray-100">{operationsQuota.unitsRemaining}</p>
                </div>
              </div>
            </div>
          )}
        </Card>

        <Card className="xl:col-span-2">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="text-lg font-semibold">Organize Activity</h2>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Recent organize-side actions and worker decisions, newest first.
              </p>
            </div>
            {activityFeed && (
              <span className="rounded-full bg-gray-100 px-3 py-1 text-xs font-semibold text-gray-700 dark:bg-gray-800 dark:text-gray-300">
                {activityFeed.totalCount} total
              </span>
            )}
          </div>

          {!activityFeed ? (
            <div className="mt-4 rounded-lg border border-dashed border-gray-300 p-4 text-sm text-gray-500 dark:border-gray-700 dark:text-gray-400">
              Checking recent organize activity…
            </div>
          ) : activityItems.length === 0 ? (
            <div className="mt-4 rounded-lg border border-dashed border-gray-300 p-4 text-sm text-gray-500 dark:border-gray-700 dark:text-gray-400">
              No organize-side activity has been recorded yet.
            </div>
          ) : (
            <div className="mt-4 space-y-3">
              {activityItems.map((item) => (
                <div
                  key={item.id}
                  className="rounded-lg border border-gray-200 p-4 dark:border-gray-800"
                >
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <p className="text-sm font-semibold text-gray-900 dark:text-gray-100">{item.message}</p>
                      <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                        {item.pipelineLabel} · {item.phase} · {new Date(item.occurredAt).toLocaleString()}
                      </p>
                    </div>
                    <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${
                      item.level === 'error'
                        ? 'bg-red-100 text-red-800 dark:bg-red-950/30 dark:text-red-300'
                        : item.level === 'warning'
                        ? 'bg-orange-100 text-orange-800 dark:bg-orange-950/30 dark:text-orange-300'
                        : 'bg-green-100 text-green-800 dark:bg-green-950/30 dark:text-green-300'
                    }`}>
                      {item.level}
                    </span>
                  </div>
                </div>
              ))}
              {activityFeed.hasMore && (
                <div className="flex justify-end">
                  <Button variant="secondary" onClick={() => setActivityOffset(activityOffset + activityLimit)}>
                    Load Older
                  </Button>
                </div>
              )}
            </div>
          )}
        </Card>
      </div>

      <Card>
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold">Duplicate Review Queue</h2>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              Videos assigned to more than one playlist. This pass spends no YouTube quota.
            </p>
          </div>
          <span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-800 dark:bg-amber-950/30 dark:text-amber-300">
            {duplicates?.length ?? 0} flagged
          </span>
        </div>

        {!duplicates || duplicates.length === 0 ? (
          <div className="mt-4 rounded-lg border border-dashed border-gray-300 p-4 text-sm text-gray-500 dark:border-gray-700 dark:text-gray-400">
            No cross-playlist duplicates found.
          </div>
        ) : (
          <div className="mt-4 space-y-3">
            {duplicates.map((duplicate) => (
              <div
                key={duplicate.videoId}
                className="rounded-lg border border-gray-200 p-4 dark:border-gray-800"
              >
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <h3 className="font-semibold text-gray-900 dark:text-gray-100">{duplicate.title}</h3>
                    <p className="text-sm text-gray-500 dark:text-gray-400">
                      Present in {duplicate.playlistCount} playlists
                    </p>
                  </div>
                  <span className="rounded-full bg-gray-100 px-2.5 py-1 text-xs font-medium text-gray-700 dark:bg-gray-800 dark:text-gray-300">
                    {duplicate.youTubeId}
                  </span>
                </div>

                <div className="mt-3 flex flex-wrap gap-2">
                  {duplicate.playlists.map((playlist) => (
                    <span
                      key={`${duplicate.videoId}-${playlist.playlistId}`}
                      className="rounded-full bg-blue-50 px-3 py-1 text-xs font-medium text-blue-700 dark:bg-blue-950/30 dark:text-blue-300"
                    >
                      {playlist.playlistName}
                    </span>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <Card>
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold">Remote Cleanup Plan</h2>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              Preview which YouTube playlist memberships would be removed to enforce one playlist per video.
            </p>
          </div>
          <Button
            onClick={buildRemoteCleanupPlan}
            variant="secondary"
            disabled={remoteCleanupPlan.isPending}
          >
            {remoteCleanupPlan.isPending ? 'Planning...' : 'Plan Remote Cleanup'}
          </Button>
        </div>

        {!remoteCleanupPlan.data || remoteCleanupPlan.data.length === 0 ? (
          <div className="mt-4 rounded-lg border border-dashed border-gray-300 p-4 text-sm text-gray-500 dark:border-gray-700 dark:text-gray-400">
            No remote cleanup plan built yet.
          </div>
        ) : (
          <div className="mt-4 space-y-3">
            <div className="flex justify-end">
              <Button
                onClick={() => setConfirmRemoteCleanupOpen(true)}
                variant="danger"
                disabled={remoteCleanupExecution.isPending || hasUnresolvedRemoteCleanupItems}
              >
                {remoteCleanupExecution.isPending ? 'Executing...' : 'Execute Remote Cleanup'}
              </Button>
            </div>
            {hasUnresolvedRemoteCleanupItems && (
              <p className="text-sm text-orange-700 dark:text-orange-300">
                Resolve missing playlist item ids before executing remote cleanup.
              </p>
            )}
            {remoteCleanupPlan.data.map((item) => (
              <div
                key={item.videoId}
                className="rounded-lg border border-gray-200 p-4 dark:border-gray-800"
              >
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <h3 className="font-semibold text-gray-900 dark:text-gray-100">{item.title}</h3>
                    <p className="text-sm text-gray-500 dark:text-gray-400">
                      Winner: {item.winnerPlaylistName}
                    </p>
                  </div>
                  {item.hasUnresolvedRemovals && (
                    <span className="rounded-full bg-orange-100 px-2.5 py-1 text-xs font-medium text-orange-800 dark:bg-orange-950/30 dark:text-orange-300">
                      Missing playlist item ids
                    </span>
                  )}
                </div>

                <div className="mt-3 space-y-2">
                  {item.loserPlaylists.map((playlist) => (
                    <div
                      key={`${item.videoId}-${playlist.playlistId}`}
                      className="rounded-md bg-gray-50 px-3 py-2 text-sm text-gray-700 dark:bg-gray-800 dark:text-gray-300"
                    >
                      Remove from {playlist.playlistName}
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}

        {remoteCleanupExecution.data && (
          <div className="mt-4 rounded-lg border border-green-200 bg-green-50 p-4 text-sm text-green-800 dark:border-green-900/50 dark:bg-green-950/20 dark:text-green-300">
            <p className="font-semibold">Execution Summary</p>
            <p className="mt-1">Executed: {remoteCleanupExecution.data.removalsExecuted}</p>
            <p className="mt-1">Skipped: {remoteCleanupExecution.data.removalsSkipped}</p>
            <p className="mt-1">Deferred: {remoteCleanupExecution.data.deferredCount}</p>
            {remoteCleanupExecution.data.runId && (
              <p className="mt-1">Run ID: {remoteCleanupExecution.data.runId}</p>
            )}
            {remoteCleanupExecution.data.deferredCount > 0 && (
              <p className="mt-3 font-medium">
                {remoteCleanupExecution.data.deferredCount} removals were deferred and should be retried after quota resets.
              </p>
            )}
            {remoteCleanupExecution.data.errors.length > 0 && (
              <div className="mt-3 space-y-1">
                {remoteCleanupExecution.data.errors.map((error, index) => (
                  <p key={`${remoteCleanupExecution.data.runId ?? 'remote-cleanup'}-${index}`}>{error}</p>
                ))}
              </div>
            )}
          </div>
        )}
      </Card>

      <Modal
        open={confirmRemoteCleanupOpen}
        onClose={() => setConfirmRemoteCleanupOpen(false)}
        title="Confirm Remote Cleanup"
      >
        <div className="space-y-4 text-sm text-gray-700 dark:text-gray-300">
          <p>
            This will remove duplicate playlist memberships on YouTube and keep only the winning playlist for each planned video.
          </p>
          <Input
            id="remote-cleanup-batch-size"
            label="Max removals this run"
            type="number"
            min={1}
            max={Math.min(totalRemoteCleanupRemovals || 1, MAX_REMOTE_CLEANUP_REMOVALS_PER_RUN)}
            value={remoteCleanupBatchSize}
            onChange={(event) => setRemoteCleanupBatchSize(Number(event.target.value) || 1)}
          />
          <div className="rounded-md bg-gray-50 p-3 text-gray-600 dark:bg-gray-900/40 dark:text-gray-300">
            <p>{remoteCleanupPlan.data?.length ?? 0} videos in plan</p>
            <p className="mt-1">{totalRemoteCleanupRemovals} removable playlist memberships available</p>
            <p className="mt-1">This run will execute up to {effectiveRemoteCleanupBatchSize} removals.</p>
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setConfirmRemoteCleanupOpen(false)}>
              Cancel
            </Button>
            <Button variant="danger" onClick={executeRemoteCleanup} disabled={remoteCleanupExecution.isPending}>
              Confirm Cleanup
            </Button>
          </div>
        </div>
      </Modal>

      <Modal
        open={confirmReclassifyOpen}
        onClose={() => setConfirmReclassifyOpen(false)}
        title="Reclassify Generated Tags"
      >
        <div className="space-y-4 text-sm text-gray-700 dark:text-gray-300">
          <p>
            This starts a database backup, clears generated tag suggestions, and rebuilds them from the current classifier. Manual tags are preserved.
          </p>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setConfirmReclassifyOpen(false)}>
              Cancel
            </Button>
            <Button variant="danger" onClick={executeReclassify} disabled={reclassifyGeneratedTags.isPending}>
              {reclassifyGeneratedTags.isPending ? 'Starting...' : 'Start Reclassify'}
            </Button>
          </div>
        </div>
      </Modal>

      {/* Metric Counters Grid */}
      {activeRun && (
        <Card>
          <h2 className="text-lg font-semibold mb-4">Pipeline Execution Metrics</h2>
          {activeRun.pipelineType === 'sync' ? (
            <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-4">
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Playlists Discovered</p>
                <p className="text-xl font-bold mt-1">{activeRun.playlistsDiscovered}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Playlists Processed</p>
                <p className="text-xl font-bold mt-1">{activeRun.playlistsProcessed}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Items Fetched</p>
                <p className="text-xl font-bold mt-1">{activeRun.playlistItemsFetched}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Unique Videos</p>
                <p className="text-xl font-bold mt-1">{activeRun.uniqueVideoIdsIdentified}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Videos Imported</p>
                <p className="text-xl font-bold mt-1">{activeRun.videosUpserted}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Links Written</p>
                <p className="text-xl font-bold mt-1">{activeRun.playlistVideoLinksWritten}</p>
              </div>
            </div>
          ) : activeRun.pipelineType === 'remote-duplicate-cleanup' ? (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Remote Cleanup Removals</p>
                <p className="text-xl font-bold mt-1">{activeRun.videosProcessed}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Skipped</p>
                <p className="text-xl font-bold mt-1">{activeRun.videosSkipped}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Deferred</p>
                <p className="text-xl font-bold mt-1">{activeRun.videosDeferred}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Errors</p>
                <p className="text-xl font-bold mt-1">{activeRun.errorsCount}</p>
              </div>
            </div>
          ) : (
            <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-7 gap-4">
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Pending Tagging</p>
                <p className="text-xl font-bold mt-1">{activeRun.videosPendingTagging}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Videos Processed</p>
                <p className="text-xl font-bold mt-1">{activeRun.videosProcessed}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Videos Tagged</p>
                <p className="text-xl font-bold mt-1">{activeRun.videosTagged}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Videos Skipped</p>
                <p className="text-xl font-bold mt-1">{activeRun.videosSkipped}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Rule Matches</p>
                <p className="text-xl font-bold mt-1">{activeRun.ruleBasedHits}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">TF-IDF Matches</p>
                <p className="text-xl font-bold mt-1">{activeRun.tfidfHits}</p>
              </div>
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <p className="text-xs text-gray-500">Ollama AI Matches</p>
                <p className="text-xl font-bold mt-1">{activeRun.ollamaHits}</p>
              </div>
            </div>
          )}
        </Card>
      )}

      {/* Events Timeline log console */}
      {selectedRunId && (
        <Card>
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <Terminal className="w-5 h-5 text-gray-500" />
            Execution Logs & Timeline
          </h2>
          <div className="bg-gray-900 text-gray-100 p-4 rounded-lg font-mono text-xs h-64 overflow-y-auto space-y-2">
            {!events || events.length === 0 ? (
              <p className="text-gray-500 italic">Waiting for events logs to stream...</p>
            ) : (
              [...events].reverse().map((ev) => (
                <div key={ev.id} className="flex items-start gap-3">
                  <span className="text-gray-500 shrink-0">{new Date(ev.occurredAt).toLocaleTimeString()}</span>
                  <span
                    className={`font-bold uppercase shrink-0 ${
                      ev.level === 'error' ? 'text-red-400' : ev.level === 'warning' ? 'text-yellow-400' : 'text-green-400'
                    }`}
                  >
                    [{ev.level}]
                  </span>
                  <span className="text-blue-400 shrink-0 capitalize">{ev.phase}</span>
                  <span className="text-gray-300">{ev.message}</span>
                </div>
              ))
            )}
          </div>
        </Card>
      )}

      {/* Historical Runs Table */}
      <Card>
        <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
          <History className="w-5 h-5 text-gray-500" />
          Recent Runs History
        </h2>
        {!history || history.length === 0 ? (
          <p className="text-gray-500 text-sm py-4">No historical runs recorded.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b dark:border-gray-800 text-gray-500 pb-2">
                  <th className="py-2.5">Pipeline Run</th>
                  <th>Status</th>
                  <th>Started</th>
                  <th>Duration</th>
                  <th>Metrics Summary</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
                {history.map((run) => (
                  <tr key={run.runId} className="hover:bg-gray-50 dark:hover:bg-gray-800/20">
                    <td className="py-2.5 font-semibold capitalize">{run.pipelineType}</td>
                    <td>{getStatusBadge(run.status)}</td>
                    <td className="text-gray-500">{new Date(run.startedAt).toLocaleString()}</td>
                    <td>{getDurationString(run)}</td>
                    <td className="text-gray-500 text-xs">{getCountersSummary(run)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}

function getResetDistance(resetsAt: string) {
  const diffMs = Math.max(0, new Date(resetsAt).getTime() - Date.now());
  const totalMinutes = Math.ceil(diffMs / 60000);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (hours <= 0) {
    return `${minutes}m`;
  }

  if (minutes === 0) {
    return `${hours}h`;
  }

  return `${hours}h ${minutes}m`;
}
