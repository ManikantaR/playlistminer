import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type { PagedResult, Video } from '@/types';

export interface VideoFilter {
  search?: string;
  tags?: number[];
  status?: string;
  playlistId?: number;
  page?: number;
  pageSize?: number;
}

export function useVideos(filter: VideoFilter = {}) {
  return useQuery({
    queryKey: ['videos', filter],
    queryFn: () =>
      apiGet<PagedResult<Video>>('/api/videos', {
        search: filter.search,
        tags: filter.tags?.join(','),
        status: filter.status,
        playlistId: filter.playlistId,
        page: filter.page ?? 1,
        pageSize: filter.pageSize ?? 20,
      }),
  });
}
