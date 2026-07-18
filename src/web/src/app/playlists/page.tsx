'use client';
import { useMemo, useState } from 'react';
import { usePlaylists, useSetInboxPlaylist } from '@/hooks/usePlaylists';
import Card from '@/components/ui/Card';
import EmptyState from '@/components/ui/EmptyState';
import { ListMusic, Search } from 'lucide-react';
import toast from 'react-hot-toast';

export default function PlaylistsPage() {
  const { data: playlists, isLoading } = usePlaylists();
  const setInboxMutation = useSetInboxPlaylist();
  const [searchTerm, setSearchTerm] = useState('');

  const filteredPlaylists = useMemo(() => {
    const query = searchTerm.trim().toLowerCase();

    if (!playlists || query.length === 0) {
      return playlists ?? [];
    }

    return playlists.filter((playlist) => (
      playlist.name.toLowerCase().includes(query)
      || playlist.youTubeId.toLowerCase().includes(query)
    ));
  }, [playlists, searchTerm]);

  const setAsInbox = async (playlistId: number) => {
    try {
      await setInboxMutation.mutateAsync(playlistId);
      toast.success('Inbox playlist updated');
    } catch {
      toast.error('Failed to set inbox');
    }
  };

  if (isLoading) return <div className="animate-pulse text-gray-500 p-8">Loading…</div>;
  if (!playlists || playlists.length === 0) {
    return <EmptyState title="No playlists" message="No playlists found." />;
  }

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <h1 className="text-2xl font-bold">Playlists</h1>
        <label className="relative block sm:w-80">
          <span className="sr-only">Search playlists</span>
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
          <input
            type="search"
            value={searchTerm}
            onChange={(event) => setSearchTerm(event.target.value)}
            aria-label="Search playlists"
            placeholder="Search playlists"
            className="w-full rounded-lg border border-gray-300 bg-white py-2 pl-9 pr-3 text-sm dark:border-gray-700 dark:bg-gray-900"
          />
        </label>
      </div>
      {filteredPlaylists.length === 0 ? (
        <EmptyState title="No matching playlists" message="Try a different playlist name or YouTube ID." />
      ) : filteredPlaylists.map((pl) => (
        <Card key={pl.youTubeId} className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <ListMusic className="w-5 h-5 text-gray-400" />
            <div>
              <div className="flex items-center gap-2">
                <span className="font-medium">{pl.name}</span>
                {pl.isInbox && (
                  <span className="px-2 py-0.5 bg-blue-100 text-blue-700 rounded-full text-xs">
                    Inbox
                  </span>
                )}
              </div>
              <span className="text-sm text-gray-500">{pl.itemCount} videos</span>
            </div>
          </div>
          {!pl.isInbox && (
            <button
              onClick={() => setAsInbox(pl.id)}
              disabled={setInboxMutation.isPending}
              aria-label={`Set ${pl.name} as inbox`}
              className="text-sm text-blue-600 hover:underline"
            >
              Set as Inbox
            </button>
          )}
        </Card>
      ))}
    </div>
  );
}
