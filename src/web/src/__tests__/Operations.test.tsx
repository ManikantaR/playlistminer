import { render, screen } from '@testing-library/react';
import React from 'react';

jest.mock('@/hooks/usePipeline');

import {
  usePipelineStatus,
  usePipelineHistory,
  usePipelineEvents,
  usePipelineHealth
} from '@/hooks/usePipeline';
import type { PipelineRun, PipelineEvent, DependencyHealth } from '@/types';
import OperationsPage from '../app/operations/page';

const mockUsePipelineStatus = usePipelineStatus as jest.MockedFunction<typeof usePipelineStatus>;
const mockUsePipelineHistory = usePipelineHistory as jest.MockedFunction<typeof usePipelineHistory>;
const mockUsePipelineEvents = usePipelineEvents as jest.MockedFunction<typeof usePipelineEvents>;
const mockUsePipelineHealth = usePipelineHealth as jest.MockedFunction<typeof usePipelineHealth>;

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

describe('OperationsPage', () => {
  const sampleHealth: DependencyHealth = {
    database: 'healthy',
    oauthConnected: true,
    youtubeQuotaAvailable: true,
    ollamaReachable: true,
    workerStatus: 'healthy',
    workerLastHeartbeat: new Date().toISOString(),
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

  beforeEach(() => {
    jest.clearAllMocks();
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
});
