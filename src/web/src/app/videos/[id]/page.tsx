'use client';
import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useVideo } from '@/hooks/useVideo';
import { useTags } from '@/hooks/useTags';
import { useAcceptSuggestion, useRejectSuggestion, useUpdateTags } from '@/hooks/useMutations';
import Badge from '@/components/ui/Badge';
import Button from '@/components/ui/Button';
import Modal from '@/components/ui/Modal';
import { ExternalLink, Check, X } from 'lucide-react';
import toast from 'react-hot-toast';
import type { TagSuggestion } from '@/types';
import Image from 'next/image';

export default function VideoDetailPage() {
  const { id } = useParams<{ id: string }>();
  const videoId = parseInt(id, 10);
  const { data: video, isLoading } = useVideo(videoId);
  const { data: allTags } = useTags();
  const [tagModalOpen, setTagModalOpen] = useState(false);
  const [tagSearch, setTagSearch] = useState('');
  const accept = useAcceptSuggestion();
  const reject = useRejectSuggestion();
  const updateTags = useUpdateTags();

  if (isLoading) return <div className="animate-pulse p-8 text-gray-500">Loading…</div>;
  if (!video) return <div className="p-8 text-red-500">Video not found.</div>;

  const manualTags = video.tags.filter((t) => t.source === 'Manual');
  const suggestions = video.tags.filter((t) => t.source !== 'Manual');

  const handleAccept = async (tag: TagSuggestion) => {
    try {
      await accept.mutateAsync({ videoId, tagId: tag.tagId });
      toast.success(`Accepted "${tag.tagName}"`);
    } catch {
      toast.error('Failed to accept suggestion');
    }
  };

  const handleReject = async (tag: TagSuggestion) => {
    try {
      await reject.mutateAsync({ videoId, tagId: tag.tagId });
      toast.success(`Rejected "${tag.tagName}"`);
    } catch {
      toast.error('Failed to reject suggestion');
    }
  };

  const handleRemoveTag = async (tagId: number) => {
    const remaining = manualTags.filter((t) => t.tagId !== tagId).map((t) => t.tagId);
    try {
      await updateTags.mutateAsync({ videoId, tagIds: remaining });
      toast.success('Tag removed');
    } catch {
      toast.error('Failed to remove tag');
    }
  };

  const handleAddTag = async (tagId: number) => {
    const tagIds = [...manualTags.map((t) => t.tagId), tagId];
    try {
      await updateTags.mutateAsync({ videoId, tagIds });
      toast.success('Tag added');
      setTagModalOpen(false);
    } catch {
      toast.error('Failed to add tag');
    }
  };

  const filteredTags = allTags?.filter(
    (t) =>
      t.name.toLowerCase().includes(tagSearch.toLowerCase()) &&
      !video.tags.some((vt) => vt.tagId === t.id),
  ) ?? [];

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <div className="flex gap-6">
        {video.thumbnailUrl && (
          <Image
            src={video.thumbnailUrl}
            alt={video.title}
            width={320}
            height={180}
            className="rounded-lg object-cover flex-shrink-0"
          />
        )}
        <div className="flex-1 space-y-2">
          <h1 className="text-2xl font-bold">{video.title}</h1>
          <p className="text-gray-600 dark:text-gray-400">{video.channelName}</p>
          <p className="text-sm text-gray-500">
            {new Date(video.publishedAt).toLocaleDateString()} · {video.duration}
          </p>
          <a
            href={`https://youtube.com/watch?v=${video.youTubeId}`}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1 text-sm text-blue-600 hover:underline"
          >
            Watch on YouTube <ExternalLink className="w-3 h-3" />
          </a>
        </div>
      </div>

      <section>
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-lg font-semibold">Current Tags</h2>
          <Button size="sm" variant="secondary" onClick={() => setTagModalOpen(true)}>
            + Add Tag
          </Button>
        </div>
        {manualTags.length === 0 ? (
          <p className="text-sm text-gray-500">No tags yet.</p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {manualTags.map((tag) => (
              <Badge
                key={tag.tagId}
                label={tag.tagName}
                source={tag.source}
                onRemove={() => handleRemoveTag(tag.tagId)}
              />
            ))}
          </div>
        )}
      </section>

      {suggestions.length > 0 && (
        <section>
          <h2 className="text-lg font-semibold mb-3">Suggested Tags</h2>
          <div className="flex flex-wrap gap-2">
            {suggestions.map((tag) => (
              <div key={tag.tagId} className="flex items-center gap-1">
                <Badge
                  label={tag.tagName}
                  source={tag.source}
                  confidence={tag.confidence}
                />
                <button
                  type="button"
                  aria-label={`Accept ${tag.tagName}`}
                  onClick={() => handleAccept(tag)}
                  className="p-1 rounded text-green-600 hover:bg-green-50 dark:hover:bg-green-900/30"
                >
                  <Check className="w-4 h-4" />
                </button>
                <button
                  type="button"
                  aria-label={`Reject ${tag.tagName}`}
                  onClick={() => handleReject(tag)}
                  className="p-1 rounded text-red-500 hover:bg-red-50 dark:hover:bg-red-900/30"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
            ))}
          </div>
        </section>
      )}

      <Modal open={tagModalOpen} onClose={() => setTagModalOpen(false)} title="Add Tag">
        <input
          type="search"
          placeholder="Search tags…"
          value={tagSearch}
          onChange={(e) => setTagSearch(e.target.value)}
          className="w-full px-3 py-2 mb-3 rounded border border-gray-300 dark:border-gray-600 dark:bg-gray-700 text-sm"
        />
        <div className="space-y-1 max-h-64 overflow-y-auto">
          {filteredTags.map((tag) => (
            <button
              key={tag.id}
              onClick={() => handleAddTag(tag.id)}
              className="w-full text-left px-3 py-2 rounded hover:bg-gray-100 dark:hover:bg-gray-700 text-sm"
            >
              {tag.name}
              {tag.category && <span className="text-gray-400 ml-2 text-xs">({tag.category})</span>}
            </button>
          ))}
          {filteredTags.length === 0 && (
            <p className="text-sm text-gray-500 py-4 text-center">No tags available.</p>
          )}
        </div>
      </Modal>
    </div>
  );
}
