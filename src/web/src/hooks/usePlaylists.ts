import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/lib/api-client';
import type { Playlist } from '@/types';

export function usePlaylists() {
  return useQuery({
    queryKey: ['playlists'],
    queryFn: () => apiGet<Playlist[]>('/api/playlists'),
  });
}

export function useSetInboxPlaylist() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (playlistId: number) => apiPost<void>(`/api/playlists/${playlistId}/set-inbox`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['playlists'] });
    },
  });
}
