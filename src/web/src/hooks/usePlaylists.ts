import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type { Playlist } from '@/types';

export function usePlaylists() {
  return useQuery({
    queryKey: ['playlists'],
    queryFn: () => apiGet<Playlist[]>('/api/playlists'),
  });
}
