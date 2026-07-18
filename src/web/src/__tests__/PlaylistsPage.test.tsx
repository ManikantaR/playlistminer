import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';

jest.mock('@/hooks/usePlaylists');
jest.mock('react-hot-toast', () => ({
  success: jest.fn(),
  error: jest.fn(),
}));

import { usePlaylists, useSetInboxPlaylist } from '@/hooks/usePlaylists';
import PlaylistsPage from '../app/playlists/page';
import type { Playlist } from '@/types';
import toast from 'react-hot-toast';

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
      <PlaylistsPage />
    </QueryClientProvider>,
  );
};

describe('PlaylistsPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders inbox badge and action button state', () => {
    mockUsePlaylists.mockReturnValue(
      makeQueryResult([
        makePlaylist({ id: 1, name: 'Incoming', isInbox: true }),
        makePlaylist({ id: 2, name: 'Programming', youTubeId: 'PL0002' }),
      ]) as ReturnType<typeof usePlaylists>,
    );
    mockUseSetInboxPlaylist.mockReturnValue({
      mutateAsync: jest.fn(),
      isPending: false,
    } as ReturnType<typeof useSetInboxPlaylist>);

    renderPage();

    expect(screen.getByText('Inbox')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /set incoming as inbox/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /set programming as inbox/i })).toBeInTheDocument();
  });

  it('filters playlists by name so myinbox is easy to find', () => {
    mockUsePlaylists.mockReturnValue(
      makeQueryResult([
        makePlaylist({ id: 1, name: 'AI Skills', youTubeId: 'PL0001' }),
        makePlaylist({ id: 407, name: 'myinbox', youTubeId: 'PLH_QpnlkswM8', itemCount: 7 }),
      ]) as ReturnType<typeof usePlaylists>,
    );
    mockUseSetInboxPlaylist.mockReturnValue({
      mutateAsync: jest.fn(),
      isPending: false,
    } as ReturnType<typeof useSetInboxPlaylist>);

    renderPage();
    fireEvent.change(screen.getByLabelText('Search playlists'), { target: { value: 'MYINBOX' } });

    expect(screen.getByText('myinbox')).toBeInTheDocument();
    expect(screen.getByText('7 videos')).toBeInTheDocument();
    expect(screen.queryByText('AI Skills')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /set myinbox as inbox/i })).toBeInTheDocument();
  });

  it('sets a playlist as inbox and shows success feedback', async () => {
    const mutateAsync = jest.fn().mockResolvedValue(undefined);

    mockUsePlaylists.mockReturnValue(
      makeQueryResult([makePlaylist({ id: 7, name: 'Programming' })]) as ReturnType<typeof usePlaylists>,
    );
    mockUseSetInboxPlaylist.mockReturnValue({
      mutateAsync,
      isPending: false,
    } as ReturnType<typeof useSetInboxPlaylist>);

    renderPage();
    fireEvent.click(screen.getByRole('button', { name: /set programming as inbox/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith(7));
    expect(mockToast.success).toHaveBeenCalledWith('Inbox playlist updated');
  });

  it('shows an error toast when setting inbox fails', async () => {
    const mutateAsync = jest.fn().mockRejectedValue(new Error('boom'));

    mockUsePlaylists.mockReturnValue(
      makeQueryResult([makePlaylist({ id: 7, name: 'Programming' })]) as ReturnType<typeof usePlaylists>,
    );
    mockUseSetInboxPlaylist.mockReturnValue({
      mutateAsync,
      isPending: false,
    } as ReturnType<typeof useSetInboxPlaylist>);

    renderPage();
    fireEvent.click(screen.getByRole('button', { name: /set programming as inbox/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith(7));
    expect(mockToast.error).toHaveBeenCalledWith('Failed to set inbox');
  });
});
