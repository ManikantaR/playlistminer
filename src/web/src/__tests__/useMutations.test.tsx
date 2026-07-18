import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import React from 'react';

jest.mock('@/lib/api-client', () => ({
  apiPost: jest.fn(),
  apiPatch: jest.fn(),
  apiDelete: jest.fn(),
}));

import { apiPost } from '@/lib/api-client';
import { useTriggerSync } from '@/hooks/useMutations';

const mockApiPost = apiPost as jest.MockedFunction<typeof apiPost>;

describe('useTriggerSync', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('refreshes sync state and playlists after a manual sync is triggered', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const queryClient = new QueryClient();
    const invalidateQueries = jest.spyOn(queryClient, 'invalidateQueries');

    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );

    const { result } = renderHook(() => useTriggerSync(), { wrapper });

    await act(async () => {
      await result.current.mutateAsync();
    });

    await waitFor(() => {
      expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['syncStatus'] });
      expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['syncHistory'] });
      expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['playlists'] });
    });
  });
});
