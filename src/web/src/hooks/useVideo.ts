import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type { VideoDetail } from '@/types';

export function useVideo(id: number) {
  return useQuery({
    queryKey: ['video', id],
    queryFn: () => apiGet<VideoDetail>(`/api/videos/${id}`),
    enabled: id > 0,
  });
}
