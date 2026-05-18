'use client';
import { useEffect, useState, useCallback } from 'react';
import { useVideos } from '@/hooks/useVideos';
import { useAcceptSuggestion, useRejectSuggestion } from '@/hooks/useMutations';
import Badge from '@/components/ui/Badge';
import Button from '@/components/ui/Button';
import EmptyState from '@/components/ui/EmptyState';
import { Check, X } from 'lucide-react';
import toast from 'react-hot-toast';
import type { Video, TagSuggestion } from '@/types';

export default function SuggestionsPage() {
  const { data, isLoading } = useVideos({ pageSize: 50 });
  const accept = useAcceptSuggestion();
  const reject = useRejectSuggestion();
  const [activeIndex, setActiveIndex] = useState(0);

  const videosWithSuggestions = (data?.items ?? []).filter((v) =>
    v.tags.some((t) => t.source !== 'Manual'),
  );

  const allSuggestions = videosWithSuggestions.flatMap((v) =>
    v.tags
      .filter((t) => t.source !== 'Manual')
      .map((t) => ({ video: v, tag: t })),
  );

  const handleAccept = useCallback(
    async (video: Video, tag: TagSuggestion) => {
      try {
        await accept.mutateAsync({ videoId: video.id, tagId: tag.tagId });
        toast.success(`Accepted "${tag.tagName}"`);
      } catch {
        toast.error('Failed');
      }
    },
    [accept],
  );

  const handleReject = useCallback(
    async (video: Video, tag: TagSuggestion) => {
      try {
        await reject.mutateAsync({ videoId: video.id, tagId: tag.tagId });
        toast.success(`Rejected "${tag.tagName}"`);
      } catch {
        toast.error('Failed');
      }
    },
    [reject],
  );

  const handleAcceptAllHighConfidence = async () => {
    const highConf = allSuggestions.filter(
      (s) => s.tag.confidence !== null && s.tag.confidence > 0.8,
    );
    await Promise.all(highConf.map(({ video, tag }) => handleAccept(video, tag)));
    toast.success(`Accepted ${highConf.length} high-confidence suggestions`);
  };

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'j') setActiveIndex((i) => Math.min(i + 1, allSuggestions.length - 1));
      if (e.key === 'k') setActiveIndex((i) => Math.max(i - 1, 0));
      if (e.key === 'y' && allSuggestions[activeIndex]) {
        const { video, tag } = allSuggestions[activeIndex];
        handleAccept(video, tag);
      }
      if (e.key === 'n' && allSuggestions[activeIndex]) {
        const { video, tag } = allSuggestions[activeIndex];
        handleReject(video, tag);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [allSuggestions, activeIndex, handleAccept, handleReject]);

  if (isLoading) return <div className="animate-pulse text-gray-500 p-8">Loading…</div>;

  if (videosWithSuggestions.length === 0) {
    return (
      <EmptyState
        title="No pending suggestions"
        message="All suggestions have been reviewed. Great job!"
      />
    );
  }

  return (
    <div className="max-w-4xl mx-auto space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Suggestions</h1>
        <div className="flex items-center gap-3">
          <span className="text-xs text-gray-500 hidden sm:block">
            j/k navigate · y accept · n reject
          </span>
          <Button variant="secondary" size="sm" onClick={handleAcceptAllHighConfidence}>
            Accept All High Confidence
          </Button>
        </div>
      </div>

      {videosWithSuggestions.map((video) => {
        const videoSuggestions = video.tags.filter((t) => t.source !== 'Manual');
        if (videoSuggestions.length === 0) return null;
        return (
          <div
            key={video.id}
            className="bg-white dark:bg-gray-800 rounded-lg shadow p-4 space-y-3"
          >
            <h3 className="font-medium text-sm line-clamp-1">{video.title}</h3>
            <div className="flex flex-wrap gap-2">
              {videoSuggestions.map((tag, i) => {
                const flatIdx = allSuggestions.findIndex(
                  (s) => s.video.id === video.id && s.tag.tagId === tag.tagId,
                );
                const isActive = flatIdx === activeIndex;
                return (
                  <div
                    key={tag.tagId}
                    className={`flex items-center gap-1 rounded p-1 ${isActive ? 'ring-2 ring-blue-400' : ''}`}
                    data-testid={`suggestion-${video.id}-${tag.tagId}`}
                  >
                    <Badge label={tag.tagName} source={tag.source} confidence={tag.confidence} />
                    <button
                      type="button"
                      aria-label={`Accept ${tag.tagName}`}
                      onClick={() => handleAccept(video, tag)}
                      className="p-1 rounded text-green-600 hover:bg-green-50 dark:hover:bg-green-900/30"
                    >
                      <Check className="w-4 h-4" />
                    </button>
                    <button
                      type="button"
                      aria-label={`Reject ${tag.tagName}`}
                      onClick={() => handleReject(video, tag)}
                      className="p-1 rounded text-red-500 hover:bg-red-50 dark:hover:bg-red-900/30"
                    >
                      <X className="w-4 h-4" />
                    </button>
                  </div>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}
