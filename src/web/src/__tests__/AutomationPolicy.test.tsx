import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';

jest.mock('@/hooks/useOAuth');
jest.mock('@/hooks/useSync');
jest.mock('@/hooks/usePlaylists');
jest.mock('@/hooks/useAutomationPolicy');
jest.mock('@/hooks/useOperations');
jest.mock('@/hooks/usePipeline');
jest.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams(),
}));
jest.mock('react-hot-toast', () => ({
  success: jest.fn(),
  error: jest.fn(),
}));

import SettingsPage from '@/app/settings/page';
import { useAutomationPolicy, useUpdateAutomationPolicy } from '@/hooks/useAutomationPolicy';
import { useOperationsQuota } from '@/hooks/useOperations';
import { usePipelineHistory } from '@/hooks/usePipeline';
import { useConnect, useDisconnect, useOAuthStatus } from '@/hooks/useOAuth';
import { usePlaylists, useSetInboxPlaylist } from '@/hooks/usePlaylists';
import { useSyncStatus } from '@/hooks/useSync';
import type { AutomationPolicy, PipelineRun } from '@/types';
import toast from 'react-hot-toast';

const mockUseAutomationPolicy = useAutomationPolicy as jest.MockedFunction<typeof useAutomationPolicy>;
const mockUseUpdateAutomationPolicy = useUpdateAutomationPolicy as jest.MockedFunction<typeof useUpdateAutomationPolicy>;
const mockUseOperationsQuota = useOperationsQuota as jest.MockedFunction<typeof useOperationsQuota>;
const mockUsePipelineHistory = usePipelineHistory as jest.MockedFunction<typeof usePipelineHistory>;
const mockUseOAuthStatus = useOAuthStatus as jest.MockedFunction<typeof useOAuthStatus>;
const mockUseConnect = useConnect as jest.MockedFunction<typeof useConnect>;
const mockUseDisconnect = useDisconnect as jest.MockedFunction<typeof useDisconnect>;
const mockUseSyncStatus = useSyncStatus as jest.MockedFunction<typeof useSyncStatus>;
const mockUsePlaylists = usePlaylists as jest.MockedFunction<typeof usePlaylists>;
const mockUseSetInboxPlaylist = useSetInboxPlaylist as jest.MockedFunction<typeof useSetInboxPlaylist>;
const mockToast = toast as jest.Mocked<typeof toast>;

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

const policy: AutomationPolicy = {
  mode: 'manual',
  highConfidenceThreshold: 0.9,
  reviewThreshold: 0.65,
  dailyMoveBudget: 80,
  nightlyRestoreBudget: 150,
  cleanupRecommendationCount: 5,
  offPeakWindowStart: '23:00',
  offPeakWindowEnd: '05:00',
  publicAiFallbackEnabled: false,
  publicAiProvider: null,
  publicAiModel: null,
  transcriptCloudPolicy: 'never',
  isPaused: false,
};

const lastRun = {
  runId: 'run-34',
  pipelineType: 'organize-execute',
  status: 'completed',
  phase: 'completed',
  startedAt: '2026-07-18T03:30:00Z',
  updatedAt: '2026-07-18T03:35:00Z',
  completedAt: '2026-07-18T03:35:00Z',
  currentMessage: null,
  error: null,
  playlistsDiscovered: 0,
  playlistsProcessed: 0,
  playlistItemsFetched: 0,
  uniqueVideoIdsIdentified: 0,
  videoMetadataBatchesTotal: 0,
  videoMetadataBatchesCompleted: 0,
  videosUpserted: 0,
  playlistVideoLinksWritten: 0,
  videosArchived: 0,
  videosDeferred: 0,
  errorsCount: 0,
  videosPendingTagging: 0,
  videosProcessed: 9,
  videosTagged: 0,
  videosSkipped: 0,
  ruleBasedHits: 0,
  tfidfHits: 0,
  ollamaHits: 0,
} satisfies PipelineRun;

const renderPage = () => {
  const queryClient = new QueryClient();

  return render(
    <QueryClientProvider client={queryClient}>
      <SettingsPage />
    </QueryClientProvider>,
  );
};

describe('Automation policy settings', () => {
  beforeEach(() => {
    jest.clearAllMocks();

    mockUseSyncStatus.mockReturnValue(
      makeQueryResult({ isRunning: false, lastSync: null }) as ReturnType<typeof useSyncStatus>,
    );
    mockUseOAuthStatus.mockReturnValue(
      makeQueryResult({ connected: true }) as ReturnType<typeof useOAuthStatus>,
    );
    mockUsePlaylists.mockReturnValue(
      makeQueryResult([]) as ReturnType<typeof usePlaylists>,
    );
    mockUseConnect.mockReturnValue({
      mutate: jest.fn(),
      isPending: false,
    } as ReturnType<typeof useConnect>);
    mockUseDisconnect.mockReturnValue({
      mutate: jest.fn(),
      isPending: false,
    } as ReturnType<typeof useDisconnect>);
    mockUseSetInboxPlaylist.mockReturnValue({
      mutateAsync: jest.fn(),
      isPending: false,
    } as ReturnType<typeof useSetInboxPlaylist>);
    mockUseOperationsQuota.mockReturnValue(
      makeQueryResult({
        movesUsedToday: 12,
        moveBudget: 80,
        resetsAt: '2026-07-19T07:00:00Z',
        unitsRemaining: 68,
        isBlocked: false,
        message: 'Move budget available.',
      }) as ReturnType<typeof useOperationsQuota>,
    );
    mockUsePipelineHistory.mockReturnValue(
      makeQueryResult([lastRun]) as ReturnType<typeof usePipelineHistory>,
    );
  });

  it('loads persisted automation policy controls and saves updates', async () => {
    const mutateAsync = jest.fn().mockResolvedValue({ ...policy, mode: 'first_week_approval' });

    mockUseAutomationPolicy.mockReturnValue(
      makeQueryResult(policy) as ReturnType<typeof useAutomationPolicy>,
    );
    mockUseUpdateAutomationPolicy.mockReturnValue({
      mutateAsync,
      isPending: false,
    } as ReturnType<typeof useUpdateAutomationPolicy>);

    renderPage();

    expect(screen.getByRole('heading', { name: 'Automation Policy' })).toBeInTheDocument();
    expect(screen.getByLabelText('Automation mode')).toHaveValue('manual');
    expect(screen.getByLabelText('Off-peak start')).toHaveValue('23:00');
    expect(screen.getByLabelText('Off-peak end')).toHaveValue('05:00');
    expect(screen.getByLabelText('Daily move budget')).toHaveValue(80);
    expect(screen.getByText('Quota remaining')).toBeInTheDocument();
    expect(screen.getByText('68 / 80')).toBeInTheDocument();
    expect(screen.getByText('Pending approvals')).toBeInTheDocument();
    expect(screen.getByText('0')).toBeInTheDocument();
    expect(screen.getByText('Last automation run')).toBeInTheDocument();
    expect(screen.getByText(/completed/)).toBeInTheDocument();
    expect(screen.getByText('Next scheduled run')).toBeInTheDocument();
    expect(screen.getByText(/23:00/)).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Automation mode'), { target: { value: 'first_week_approval' } });
    fireEvent.click(screen.getByLabelText('Pause automation'));
    fireEvent.click(screen.getByRole('button', { name: 'Save Automation Policy' }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith({
      ...policy,
      mode: 'first_week_approval',
      isPaused: true,
    }));
    expect(mockToast.success).toHaveBeenCalledWith('Automation policy updated');
  });
});
