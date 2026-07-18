import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';

jest.mock('@/hooks/useOAuth');
jest.mock('@/hooks/useSync');
jest.mock('@/hooks/usePlaylists');
jest.mock('@/hooks/useAutomationPolicy');
jest.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams(),
}));
jest.mock('react-hot-toast', () => ({
  success: jest.fn(),
  error: jest.fn(),
}));

import SettingsPage from '@/app/settings/page';
import { useAutomationPolicy, useUpdateAutomationPolicy } from '@/hooks/useAutomationPolicy';
import { useConnect, useDisconnect, useOAuthStatus } from '@/hooks/useOAuth';
import { usePlaylists, useSetInboxPlaylist } from '@/hooks/usePlaylists';
import { useSyncStatus } from '@/hooks/useSync';
import type { AutomationPolicy, Playlist } from '@/types';
import toast from 'react-hot-toast';

const mockUseAutomationPolicy = useAutomationPolicy as jest.MockedFunction<typeof useAutomationPolicy>;
const mockUseUpdateAutomationPolicy = useUpdateAutomationPolicy as jest.MockedFunction<typeof useUpdateAutomationPolicy>;
const mockUseOAuthStatus = useOAuthStatus as jest.MockedFunction<typeof useOAuthStatus>;
const mockUseConnect = useConnect as jest.MockedFunction<typeof useConnect>;
const mockUseDisconnect = useDisconnect as jest.MockedFunction<typeof useDisconnect>;
const mockUseSyncStatus = useSyncStatus as jest.MockedFunction<typeof useSyncStatus>;
const mockUsePlaylists = usePlaylists as jest.MockedFunction<typeof usePlaylists>;
const mockUseSetInboxPlaylist = useSetInboxPlaylist as jest.MockedFunction<typeof useSetInboxPlaylist>;
const mockToast = toast as jest.Mocked<typeof toast>;

const makePlaylist = (overrides: Partial<Playlist> = {}): Playlist => ({
  id: 1,
  youTubeId: 'PL0001',
  name: 'Incoming',
  description: null,
  isInbox: false,
  itemCount: 12,
  ...overrides,
});

const automationPolicy: AutomationPolicy = {
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

const renderPage = () => {
  const queryClient = new QueryClient();

  return render(
    <QueryClientProvider client={queryClient}>
      <SettingsPage />
    </QueryClientProvider>,
  );
};

describe('SettingsPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();

    mockUseSyncStatus.mockReturnValue(
      makeQueryResult({ isRunning: false, lastSync: null }) as ReturnType<typeof useSyncStatus>,
    );
    mockUseOAuthStatus.mockReturnValue(
      makeQueryResult({ connected: true }) as ReturnType<typeof useOAuthStatus>,
    );
    mockUseConnect.mockReturnValue({
      mutate: jest.fn(),
      isPending: false,
    } as ReturnType<typeof useConnect>);
    mockUseDisconnect.mockReturnValue({
      mutate: jest.fn(),
      isPending: false,
    } as ReturnType<typeof useDisconnect>);
    mockUseAutomationPolicy.mockReturnValue(
      makeQueryResult(automationPolicy) as ReturnType<typeof useAutomationPolicy>,
    );
    mockUseUpdateAutomationPolicy.mockReturnValue({
      mutateAsync: jest.fn(),
      isPending: false,
    } as ReturnType<typeof useUpdateAutomationPolicy>);
  });

  it('shows the current inbox and lets the user choose another synced playlist', async () => {
    const mutateAsync = jest.fn().mockResolvedValue(undefined);

    mockUsePlaylists.mockReturnValue(
      makeQueryResult([
        makePlaylist({ id: 1, name: 'Incoming', isInbox: true }),
        makePlaylist({ id: 2, name: 'Programming', youTubeId: 'PL0002' }),
      ]) as ReturnType<typeof usePlaylists>,
    );
    mockUseSetInboxPlaylist.mockReturnValue({
      mutateAsync,
      isPending: false,
    } as ReturnType<typeof useSetInboxPlaylist>);

    renderPage();

    expect(screen.getByText('Current inbox')).toBeInTheDocument();
    expect(screen.getByLabelText('Incoming playlist')).toHaveValue('2');

    fireEvent.change(screen.getByLabelText('Incoming playlist'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Set as Incoming' }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith(2));
    expect(mockToast.success).toHaveBeenCalledWith('Inbox playlist updated');
  });

  it('suggests synced inbox-like playlists and can set myinbox as incoming', async () => {
    const mutateAsync = jest.fn().mockResolvedValue(undefined);

    mockUsePlaylists.mockReturnValue(
      makeQueryResult([
        makePlaylist({ id: 10, name: 'AI Skills', youTubeId: 'PL0010' }),
        makePlaylist({ id: 407, name: 'myinbox', youTubeId: 'PLH_QpnlkswM8', itemCount: 7 }),
      ]) as ReturnType<typeof usePlaylists>,
    );
    mockUseSetInboxPlaylist.mockReturnValue({
      mutateAsync,
      isPending: false,
    } as ReturnType<typeof useSetInboxPlaylist>);

    renderPage();

    expect(screen.getByText('Suggested incoming playlists')).toBeInTheDocument();
    expect(screen.getAllByText('myinbox')).toHaveLength(2);
    expect(screen.getByLabelText('Incoming playlist')).toHaveValue('407');

    fireEvent.click(screen.getByRole('button', { name: /set myinbox as incoming/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith(407));
    expect(mockToast.success).toHaveBeenCalledWith('Inbox playlist updated');
  });

  it('shows an error toast when updating the inbox fails', async () => {
    const mutateAsync = jest.fn().mockRejectedValue(new Error('boom'));

    mockUsePlaylists.mockReturnValue(
      makeQueryResult([makePlaylist({ id: 7, name: 'Programming' })]) as ReturnType<typeof usePlaylists>,
    );
    mockUseSetInboxPlaylist.mockReturnValue({
      mutateAsync,
      isPending: false,
    } as ReturnType<typeof useSetInboxPlaylist>);

    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'Set as Incoming' }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith(7));
    expect(mockToast.error).toHaveBeenCalledWith('Failed to set inbox');
  });
});
