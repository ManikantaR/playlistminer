import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';

jest.mock('@/hooks/usePipeline');
jest.mock('@/hooks/useDuplicates');
jest.mock('@/hooks/useRemoteCleanup');
jest.mock('@/hooks/useOperations');

import {
  usePipelineStatus,
  usePipelineHistory,
  usePipelineEvents,
  usePipelineHealth,
  useOperationsHealth
} from '@/hooks/usePipeline';
import { useDuplicateReview } from '@/hooks/useDuplicates';
import { useBuildRemoteCleanupPlan, useExecuteRemoteCleanup } from '@/hooks/useRemoteCleanup';
import { useOperationsActivity, useOperationsQuota } from '@/hooks/useOperations';
import type { PipelineRun, PipelineEvent, DependencyHealth, OperationsHealth, DuplicateReview, RemoteDuplicateCleanupItem, RemoteDuplicateCleanupResult, OperationsActivityFeed, OperationsQuota } from '@/types';
import OperationsPage from '../app/operations/page';

const mockUsePipelineStatus = usePipelineStatus as jest.MockedFunction<typeof usePipelineStatus>;
const mockUsePipelineHistory = usePipelineHistory as jest.MockedFunction<typeof usePipelineHistory>;
const mockUsePipelineEvents = usePipelineEvents as jest.MockedFunction<typeof usePipelineEvents>;
const mockUsePipelineHealth = usePipelineHealth as jest.MockedFunction<typeof usePipelineHealth>;
const mockUseOperationsHealth = useOperationsHealth as jest.MockedFunction<typeof useOperationsHealth>;
const mockUseDuplicateReview = useDuplicateReview as jest.MockedFunction<typeof useDuplicateReview>;
const mockUseBuildRemoteCleanupPlan = useBuildRemoteCleanupPlan as jest.MockedFunction<typeof useBuildRemoteCleanupPlan>;
const mockUseExecuteRemoteCleanup = useExecuteRemoteCleanup as jest.MockedFunction<typeof useExecuteRemoteCleanup>;
const mockUseOperationsActivity = useOperationsActivity as jest.MockedFunction<typeof useOperationsActivity>;
const mockUseOperationsQuota = useOperationsQuota as jest.MockedFunction<typeof useOperationsQuota>;

const makeQueryResult = <T,>(data: T) => ({
  data,
  isLoading: false,
  isError: false,
  error: null,
  status: 'success' as const,
  fetchStatus: 'idle' as const,
  isPending: false,
  isSuccess: true,
  isFetching: false,
  isRefetching: false,
  isLoadingError: false,
  isRefetchError: false,
  isPlaceholderData: false,
  dataUpdatedAt: Date.now(),
  errorUpdatedAt: 0,
  failureCount: 0,
  failureReason: null,
  refetch: jest.fn(),
  isStale: false,
});

const makeLoadingQueryResult = () => ({
  data: undefined,
  isLoading: true,
  isError: false,
  error: null,
  status: 'pending' as const,
  fetchStatus: 'fetching' as const,
  isPending: true,
  isSuccess: false,
  isFetching: true,
  isRefetching: false,
  isLoadingError: false,
  isRefetchError: false,
  isPlaceholderData: false,
  dataUpdatedAt: 0,
  errorUpdatedAt: 0,
  failureCount: 0,
  failureReason: null,
  refetch: jest.fn(),
  isStale: true,
});

describe('OperationsPage', () => {
  const sampleHealth: DependencyHealth = {
    database: 'healthy',
    oauthConnected: true,
    youtubeQuotaAvailable: true,
    ollamaReachable: true,
    workerStatus: 'healthy',
    workerLastHeartbeat: new Date().toISOString(),
  };

  const sampleOperationsHealth: OperationsHealth = {
    apiHealthy: true,
    dbHealthy: true,
    workerHealthy: true,
    workerHeartbeatAgeSeconds: 5,
    oauthConnected: true,
    quotaExhausted: false,
    ollamaReachable: true,
    activeRunStalled: false,
    activeRunPhase: null,
  };

  const sampleOperationsQuota: OperationsQuota = {
    movesUsedToday: 34,
    moveBudget: 80,
    resetsAt: new Date(Date.now() + 6 * 3600 * 1000).toISOString(),
    unitsRemaining: 46,
    isBlocked: false,
    message: 'Move budget available.',
  };

  const sampleActivityFeed: OperationsActivityFeed = {
    items: [
      {
        id: 21,
        runId: 'remote-cleanup-123',
        pipelineType: 'remote-duplicate-cleanup',
        pipelineLabel: 'Remote Cleanup',
        status: 'completed',
        level: 'info',
        phase: 'completed',
        message: 'Removed duplicate video from playlist "Inbox".',
        occurredAt: new Date().toISOString(),
      },
    ],
    limit: 10,
    offset: 0,
    totalCount: 1,
    hasMore: false,
  };

  const sampleActiveSyncRun: PipelineRun = {
    runId: 'sync-run-123',
    pipelineType: 'sync',
    status: 'in_progress',
    phase: 'hydrating_video_metadata',
    startedAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    completedAt: null,
    currentMessage: 'Fetching metadata for chunk 2/5...',
    error: null,
    playlistsDiscovered: 4,
    playlistsProcessed: 2,
    playlistItemsFetched: 85,
    uniqueVideoIdsIdentified: 80,
    videoMetadataBatchesTotal: 5,
    videoMetadataBatchesCompleted: 2,
    videosUpserted: 40,
    playlistVideoLinksWritten: 40,
    videosArchived: 0,
    videosDeferred: 0,
    errorsCount: 0,
    videosPendingTagging: 0,
    videosProcessed: 0,
    videosTagged: 0,
    videosSkipped: 0,
    ruleBasedHits: 0,
    tfidfHits: 0,
    ollamaHits: 0,
  };

  const sampleEvents: PipelineEvent[] = [
    {
      id: 1,
      runId: 'sync-run-123',
      occurredAt: new Date(Date.now() - 5000).toISOString(),
      level: 'info',
      phase: 'starting',
      message: 'Sync starting',
      payloadJson: null,
    },
    {
      id: 2,
      runId: 'sync-run-123',
      occurredAt: new Date().toISOString(),
      level: 'info',
      phase: 'hydrating_video_metadata',
      message: 'Fetching metadata for chunk 2/5...',
      payloadJson: null,
    },
  ];

  const sampleDuplicates: DuplicateReview[] = [
    {
      videoId: 42,
      youTubeId: 'dupvideo01',
      title: 'Distributed Systems Deep Dive',
      thumbnailUrl: 'https://example.com/dup.jpg',
      playlistCount: 2,
      playlists: [
        { playlistId: 7, playlistName: 'AI Agents', isManaged: true, topic: 'AI Agents' },
        { playlistId: 8, playlistName: 'Backend Systems', isManaged: true, topic: 'Backend Systems' },
      ],
    },
  ];

  const sampleRemotePlan: RemoteDuplicateCleanupItem[] = [
    {
      videoId: 42,
      youTubeId: 'dupvideo01',
      title: 'Distributed Systems Deep Dive',
      winnerPlaylistId: 8,
      winnerPlaylistName: 'Backend Systems',
      hasUnresolvedRemovals: false,
      loserPlaylists: [
        { playlistId: 7, playlistName: 'AI Agents', playlistItemId: 'pli-ai' },
      ],
    },
  ];

  const sampleRemotePlanWithUnresolved: RemoteDuplicateCleanupItem[] = [
    {
      videoId: 42,
      youTubeId: 'dupvideo01',
      title: 'Distributed Systems Deep Dive',
      winnerPlaylistId: 8,
      winnerPlaylistName: 'Backend Systems',
      hasUnresolvedRemovals: true,
      loserPlaylists: [
        { playlistId: 7, playlistName: 'AI Agents', playlistItemId: null },
      ],
    },
  ];

  const sampleRemoteCleanupResult: RemoteDuplicateCleanupResult = {
    videosExamined: 1,
    removalsPlanned: 1,
    removalsExecuted: 1,
    removalsSkipped: 1,
    deferredCount: 2,
    errors: ['Missing playlist item id for one removal target.'],
    runId: 'run-123',
  };

  const sampleRemoteCleanupRun: PipelineRun = {
    ...sampleActiveSyncRun,
    runId: 'remote-cleanup-123',
    pipelineType: 'remote-duplicate-cleanup',
    phase: 'executing',
    currentMessage: 'Removed duplicate video from playlist "Inbox".',
    videosProcessed: 3,
    videosSkipped: 1,
    videosDeferred: 2,
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockUseOperationsHealth.mockReturnValue(makeQueryResult(sampleOperationsHealth));
    mockUseOperationsQuota.mockReturnValue(makeQueryResult(sampleOperationsQuota));
    mockUseOperationsActivity.mockReturnValue(makeQueryResult(sampleActivityFeed));
    mockUseDuplicateReview.mockReturnValue(makeQueryResult([]));
    mockUseBuildRemoteCleanupPlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useBuildRemoteCleanupPlan>);
    mockUseExecuteRemoteCleanup.mockReturnValue({
      mutateAsync: jest.fn(),
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useExecuteRemoteCleanup>);
  });

  it('renders idle state when no runs have executed', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));

    render(<OperationsPage />);

    expect(screen.getByText('System Operations')).toBeInTheDocument();
    expect(screen.getByText('No background runs have executed yet.')).toBeInTheDocument();
    expect(screen.getByText('Database')).toBeInTheDocument();
  });

  it('renders quota meter and recent activity feed', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));

    render(<OperationsPage />);

    expect(screen.getByText('Organize Move Budget')).toBeInTheDocument();
    expect(screen.getByText(/34 \/ 80/)).toBeInTheDocument();
    expect(screen.getByText('Organize Activity')).toBeInTheDocument();
    expect(screen.getByText('Removed duplicate video from playlist "Inbox".')).toBeInTheDocument();
  });

  it('renders neutral loading copy while move budget is still loading', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));
    mockUseOperationsQuota.mockReturnValue(makeLoadingQueryResult() as ReturnType<typeof useOperationsQuota>);

    render(<OperationsPage />);

    expect(screen.getByText('Checking move budget…')).toBeInTheDocument();
    expect(screen.queryByText('0 / 0')).not.toBeInTheDocument();
  });

  it('renders blocked quota state when the daily move budget is exhausted', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));
    mockUseOperationsQuota.mockReturnValue(makeQueryResult({
      ...sampleOperationsQuota,
      movesUsedToday: 80,
      unitsRemaining: 0,
      isBlocked: true,
      message: 'Daily move budget exhausted.',
    }));

    render(<OperationsPage />);

    expect(screen.getByText(/80 \/ 80/)).toBeInTheDocument();
    expect(screen.getByText(/blocked until reset/i)).toBeInTheDocument();
  });

  it('renders active sync run details and metrics', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult(sampleActiveSyncRun));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([sampleActiveSyncRun]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult(sampleEvents));

    render(<OperationsPage />);

    expect(screen.getByText('Active Run Progress')).toBeInTheDocument();
    expect(screen.getAllByText('Fetching metadata for chunk 2/5...').length).toBeGreaterThan(0);
    expect(screen.getAllByText('hydrating_video_metadata').length).toBeGreaterThan(0);
    expect(screen.getByText('Playlists Discovered')).toBeInTheDocument();
    expect(screen.getByText('85')).toBeInTheDocument(); // playlistItemsFetched
    expect(screen.getAllByText('40').length).toBeGreaterThan(0); // videosUpserted / links written
  });

  it('renders active remote cleanup run details with cleanup-specific labels', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult(sampleRemoteCleanupRun));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([sampleRemoteCleanupRun]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult(sampleEvents));

    render(<OperationsPage />);

    expect(screen.getByText('Removed duplicate video from playlist "Inbox".')).toBeInTheDocument();
    expect(screen.getByText('3 removals executed')).toBeInTheDocument();
    expect(screen.getByText('Remote Cleanup Removals')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders failed run state and show error details', () => {
    const failedRun: PipelineRun = {
      ...sampleActiveSyncRun,
      status: 'failed',
      error: 'YouTube API Quota exceeded limit.',
      completedAt: new Date().toISOString(),
      currentMessage: 'Run failed: YouTube API Quota exceeded limit.',
    };

    mockUsePipelineStatus.mockReturnValue(makeQueryResult(failedRun));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([failedRun]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult(sampleEvents));

    render(<OperationsPage />);

    expect(screen.getByText('Most Recent Execution')).toBeInTheDocument();
    expect(screen.getAllByText('Failed').length).toBeGreaterThan(0);
    expect(screen.getByText('Execution Failure Details')).toBeInTheDocument();
    expect(screen.getByText('YouTube API Quota exceeded limit.')).toBeInTheDocument();
  });

  it('renders dependency health sections with correct badges', () => {
    const degradedHealth: DependencyHealth = {
      database: 'unhealthy',
      oauthConnected: false,
      youtubeQuotaAvailable: false,
      ollamaReachable: false,
      workerStatus: 'stale',
      workerLastHeartbeat: new Date().toISOString(),
    };

    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(degradedHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));

    render(<OperationsPage />);

    expect(screen.getByText('Unhealthy')).toBeInTheDocument();
    expect(screen.getByText('Disconnected')).toBeInTheDocument();
    expect(screen.getByText('Exhausted')).toBeInTheDocument();
    expect(screen.getByText('Stale (Inactive)')).toBeInTheDocument();
    expect(screen.getByText('Offline (Fallback to rules/TF-IDF)')).toBeInTheDocument();
  });

  it('renders log events inside the log timeline console', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult(sampleActiveSyncRun));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([sampleActiveSyncRun]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult(sampleEvents));

    render(<OperationsPage />);

    expect(screen.getAllByText('[info]').length).toBeGreaterThan(0);
    expect(screen.getByText('Sync starting')).toBeInTheDocument();
    expect(screen.getAllByText('Fetching metadata for chunk 2/5...').length).toBeGreaterThan(0);
  });

  it('renders stalled run alert banner when a task is stalled', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult({ ...sampleActiveSyncRun, isStalled: true }));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([{ ...sampleActiveSyncRun, isStalled: true }]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult(sampleEvents));
    mockUseOperationsHealth.mockReturnValue(makeQueryResult({
      ...sampleOperationsHealth,
      activeRunStalled: true,
      activeRunPhase: 'hydrating_video_metadata'
    }));

    render(<OperationsPage />);

    expect(screen.getByText('System Stalled')).toBeInTheDocument();
    expect(screen.getByText(/has stalled/i)).toBeInTheDocument();
  });

  it('renders the duplicate review queue when managed duplicates exist', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));
    mockUseDuplicateReview.mockReturnValue(makeQueryResult(sampleDuplicates));

    render(<OperationsPage />);

    expect(screen.getByText('Duplicate Review Queue')).toBeInTheDocument();
    expect(screen.getByText('Distributed Systems Deep Dive')).toBeInTheDocument();
    expect(screen.getByText('AI Agents')).toBeInTheDocument();
    expect(screen.getByText('Backend Systems')).toBeInTheDocument();
  });

  it('renders remote cleanup plan results after building the plan', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));
    mockUseBuildRemoteCleanupPlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: sampleRemotePlan,
      isPending: false,
    } as ReturnType<typeof useBuildRemoteCleanupPlan>);

    render(<OperationsPage />);

    expect(screen.getByText('Remote Cleanup Plan')).toBeInTheDocument();
    expect(screen.getByText('Winner: Backend Systems')).toBeInTheDocument();
    expect(screen.getByText('Remove from AI Agents')).toBeInTheDocument();
  });

  it('requires confirmation before executing remote cleanup', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));
    const mutateAsync = jest.fn();
    mockUseBuildRemoteCleanupPlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: sampleRemotePlan,
      isPending: false,
    } as ReturnType<typeof useBuildRemoteCleanupPlan>);
    mockUseExecuteRemoteCleanup.mockReturnValue({
      mutateAsync,
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useExecuteRemoteCleanup>);

    render(<OperationsPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Execute Remote Cleanup' }));

    expect(screen.getByRole('dialog', { name: 'Confirm Remote Cleanup' })).toBeInTheDocument();
    expect(screen.getByText(/This will remove duplicate playlist memberships on YouTube/i)).toBeInTheDocument();
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it('submits only the configured small batch of remote cleanup removals', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));
    const mutateAsync = jest.fn();
    const multiRemovalPlan: RemoteDuplicateCleanupItem[] = [
      {
        videoId: 42,
        youTubeId: 'dupvideo01',
        title: 'Distributed Systems Deep Dive',
        winnerPlaylistId: 8,
        winnerPlaylistName: 'Backend Systems',
        hasUnresolvedRemovals: false,
        loserPlaylists: [
          { playlistId: 7, playlistName: 'AI Agents', playlistItemId: 'pli-ai' },
        ],
      },
      {
        videoId: 43,
        youTubeId: 'dupvideo02',
        title: 'Async Messaging',
        winnerPlaylistId: 10,
        winnerPlaylistName: 'Messaging',
        hasUnresolvedRemovals: false,
        loserPlaylists: [
          { playlistId: 11, playlistName: 'Queues', playlistItemId: 'pli-queue' },
          { playlistId: 12, playlistName: 'Architecture', playlistItemId: 'pli-arch' },
        ],
      },
    ];

    mockUseBuildRemoteCleanupPlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: multiRemovalPlan,
      isPending: false,
    } as ReturnType<typeof useBuildRemoteCleanupPlan>);
    mockUseExecuteRemoteCleanup.mockReturnValue({
      mutateAsync,
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useExecuteRemoteCleanup>);

    render(<OperationsPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Execute Remote Cleanup' }));
    fireEvent.change(screen.getByLabelText('Max removals this run'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Cleanup' }));

    expect(mutateAsync).toHaveBeenCalledWith([
      multiRemovalPlan[0],
      {
        ...multiRemovalPlan[1],
        loserPlaylists: [multiRemovalPlan[1].loserPlaylists[0]],
      },
    ]);
  });

  it('disables remote cleanup execution when the plan has unresolved removals', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));
    mockUseBuildRemoteCleanupPlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: sampleRemotePlanWithUnresolved,
      isPending: false,
    } as ReturnType<typeof useBuildRemoteCleanupPlan>);

    render(<OperationsPage />);

    expect(screen.getByRole('button', { name: 'Execute Remote Cleanup' })).toBeDisabled();
    expect(screen.getByText(/Resolve missing playlist item ids before executing remote cleanup/i)).toBeInTheDocument();
  });

  it('renders remote cleanup execution summary with deferred and skipped counts', () => {
    mockUsePipelineStatus.mockReturnValue(makeQueryResult<any>(null));
    mockUsePipelineHistory.mockReturnValue(makeQueryResult([]));
    mockUsePipelineHealth.mockReturnValue(makeQueryResult(sampleHealth));
    mockUsePipelineEvents.mockReturnValue(makeQueryResult([]));
    mockUseBuildRemoteCleanupPlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: sampleRemotePlan,
      isPending: false,
    } as ReturnType<typeof useBuildRemoteCleanupPlan>);
    mockUseExecuteRemoteCleanup.mockReturnValue({
      mutateAsync: jest.fn(),
      data: sampleRemoteCleanupResult,
      isPending: false,
    } as ReturnType<typeof useExecuteRemoteCleanup>);

    render(<OperationsPage />);

    expect(screen.getByText('Execution Summary')).toBeInTheDocument();
    expect(screen.getByText('Executed: 1')).toBeInTheDocument();
    expect(screen.getByText('Skipped: 1')).toBeInTheDocument();
    expect(screen.getByText('Deferred: 2')).toBeInTheDocument();
    expect(screen.getByText('Missing playlist item id for one removal target.')).toBeInTheDocument();
    expect(screen.getByText(/2 removals were deferred and should be retried after quota resets/i)).toBeInTheDocument();
  });
});
