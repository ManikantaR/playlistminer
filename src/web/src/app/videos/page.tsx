'use client';
import { useState, useCallback } from 'react';
import { useVideos } from '@/hooks/useVideos';
import { useTags } from '@/hooks/useTags';
import SearchInput from '@/components/ui/SearchInput';
import Pagination from '@/components/ui/Pagination';
import Badge from '@/components/ui/Badge';
import EmptyState from '@/components/ui/EmptyState';
import Link from 'next/link';
import Image from 'next/image';

export default function VideosPage() {
  const [search, setSearch] = useState('');
  const [selectedTags, setSelectedTags] = useState<number[]>([]);
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useVideos({ search, tags: selectedTags, status, page });
  const { data: tags } = useTags();

  const handleSearchChange = useCallback((val: string) => {
    setSearch(val);
    setPage(1);
  }, []);

  const toggleTag = (tagId: number) => {
    setSelectedTags((prev) =>
      prev.includes(tagId) ? prev.filter((t) => t !== tagId) : [...prev, tagId],
    );
    setPage(1);
  };

  return (
    <div className="max-w-6xl mx-auto space-y-4">
      <h1 className="text-2xl font-bold">Videos</h1>

      <div className="flex flex-col sm:flex-row gap-3">
        <div className="flex-1">
          <SearchInput onChange={handleSearchChange} placeholder="Search videos…" />
        </div>
        <select
          value={status}
          onChange={(e) => { setStatus(e.target.value); setPage(1); }}
          aria-label="Filter by status"
          className="px-3 py-2 rounded border border-gray-300 dark:border-gray-600 dark:bg-gray-800 text-sm"
        >
          <option value="">All statuses</option>
          <option value="Active">Active</option>
          <option value="Unavailable">Unavailable</option>
          <option value="Private">Private</option>
          <option value="Deleted">Deleted</option>
          <option value="Archived">Archived</option>
        </select>
      </div>

      {tags && tags.length > 0 && (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Filter by tag">
          {tags.slice(0, 20).map((tag) => (
            <button
              key={tag.id}
              onClick={() => toggleTag(tag.id)}
              className={`px-3 py-1 rounded-full text-xs font-medium border transition-colors ${
                selectedTags.includes(tag.id)
                  ? 'bg-blue-600 text-white border-blue-600'
                  : 'border-gray-300 text-gray-600 hover:border-blue-400 dark:border-gray-600 dark:text-gray-300'
              }`}
            >
              {tag.name}
            </button>
          ))}
        </div>
      )}

      {isLoading && (
        <div className="space-y-3" aria-label="Loading">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-16 bg-gray-200 dark:bg-gray-700 rounded animate-pulse" />
          ))}
        </div>
      )}

      {!isLoading && data && data.items.length === 0 && (
        <EmptyState title="No videos found" message="Try adjusting your search or filters." />
      )}

      {!isLoading && data && data.items.length > 0 && (
        <>
          <div className="overflow-x-auto rounded border border-gray-200 dark:border-gray-700">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 dark:bg-gray-800">
                <tr>
                  <th className="py-3 px-4 text-left text-gray-600 dark:text-gray-400 font-medium">Thumbnail</th>
                  <th className="py-3 px-4 text-left text-gray-600 dark:text-gray-400 font-medium">Title</th>
                  <th className="py-3 px-4 text-left text-gray-600 dark:text-gray-400 font-medium">Channel</th>
                  <th className="py-3 px-4 text-left text-gray-600 dark:text-gray-400 font-medium">Tags</th>
                  <th className="py-3 px-4 text-left text-gray-600 dark:text-gray-400 font-medium">Status</th>
                  <th className="py-3 px-4 text-left text-gray-600 dark:text-gray-400 font-medium">Action</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((video) => (
                  <tr key={video.id} className="border-t border-gray-100 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800/50">
                    <td className="py-2 px-4">
                      {video.thumbnailUrl ? (
                        <Image
                          src={video.thumbnailUrl}
                          alt={video.title}
                          width={80}
                          height={45}
                          className="rounded object-cover"
                        />
                      ) : (
                        <div className="w-20 h-12 bg-gray-200 dark:bg-gray-700 rounded" />
                      )}
                    </td>
                    <td className="py-2 px-4 max-w-xs">
                      <span className="line-clamp-2 dark:text-gray-200">{video.title}</span>
                    </td>
                    <td className="py-2 px-4 text-gray-600 dark:text-gray-400 whitespace-nowrap">
                      {video.channelName}
                    </td>
                    <td className="py-2 px-4">
                      <div className="flex flex-wrap gap-1">
                        {video.tags.slice(0, 3).map((tag) => (
                          <Badge
                            key={tag.tagId}
                            label={tag.tagName}
                            source={tag.source}
                            confidence={tag.confidence}
                          />
                        ))}
                      </div>
                    </td>
                    <td className="py-2 px-4">
                      <span className={`px-2 py-0.5 rounded text-xs ${
                        video.status === 'Active' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'
                      }`}>
                        {video.status}
                      </span>
                    </td>
                    <td className="py-2 px-4">
                      <Link
                        href={`/videos/${video.id}`}
                        className="text-blue-600 hover:underline text-sm"
                      >
                        View
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {data.totalPages > 1 && (
            <div className="flex justify-center">
              <Pagination
                currentPage={data.page}
                totalPages={data.totalPages}
                onPageChange={setPage}
              />
            </div>
          )}
        </>
      )}
    </div>
  );
}
