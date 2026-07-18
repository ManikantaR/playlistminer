import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPost, apiPatch, apiDelete } from '@/lib/api-client';
import type { Tag } from '@/types';

export function useAcceptSuggestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ videoId, tagId }: { videoId: number; tagId: number }) =>
      apiPost<void>(`/api/videos/${videoId}/suggestions/${tagId}/accept`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['videos'] });
      qc.invalidateQueries({ queryKey: ['video'] });
    },
  });
}

export function useRejectSuggestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ videoId, tagId }: { videoId: number; tagId: number }) =>
      apiPost<void>(`/api/videos/${videoId}/suggestions/${tagId}/reject`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['videos'] });
      qc.invalidateQueries({ queryKey: ['video'] });
    },
  });
}

export function useUpdateTags() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ videoId, tagIds }: { videoId: number; tagIds: number[] }) =>
      apiPatch<void>(`/api/videos/${videoId}`, { tagIds }),
    onSuccess: (_data, { videoId }) => {
      qc.invalidateQueries({ queryKey: ['videos'] });
      qc.invalidateQueries({ queryKey: ['video', videoId] });
    },
  });
}

export function useCreateTag() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: { name: string; category?: string }) =>
      apiPost<Tag>('/api/tags', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tags'] }),
  });
}

export function useDeleteTag() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (tagId: number) => apiDelete(`/api/tags/${tagId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tags'] }),
  });
}

export function useTriggerSync() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => apiPost<void>('/api/sync/trigger'),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['syncStatus'] });
      qc.invalidateQueries({ queryKey: ['syncHistory'] });
      qc.invalidateQueries({ queryKey: ['playlists'] });
    },
  });
}

export function useUndoAction() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => apiPost<void>(`/api/undo/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['undoLogs'] }),
  });
}

export function useCreateTagRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      tagId,
      data,
    }: {
      tagId: number;
      data: { keyword: string; field: 'Title' | 'Description' | 'Both'; weight: number };
    }) => apiPost<void>(`/api/tags/${tagId}/rules`, data),
    onSuccess: (_data, { tagId }) => qc.invalidateQueries({ queryKey: ['tagRules', tagId] }),
  });
}

export function useDeleteTagRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ tagId, ruleId }: { tagId: number; ruleId: number }) =>
      apiDelete(`/api/tags/${tagId}/rules/${ruleId}`),
    onSuccess: (_data, { tagId }) => qc.invalidateQueries({ queryKey: ['tagRules', tagId] }),
  });
}

export function useVideoSuggestions() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      videoId,
      tagIds,
      action,
    }: {
      videoId: number;
      tagIds: number[];
      action: 'accept' | 'reject';
    }) =>
      Promise.all(
        tagIds.map((tagId) =>
          apiPost<void>(`/api/videos/${videoId}/suggestions/${tagId}/${action}`),
        ),
      ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['videos'] });
      qc.invalidateQueries({ queryKey: ['video'] });
    },
  });
}
