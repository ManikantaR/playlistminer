'use client';
import { usePlaylists } from '@/hooks/usePlaylists';
import Card from '@/components/ui/Card';
import EmptyState from '@/components/ui/EmptyState';
import { ListMusic } from 'lucide-react';
import { apiPost } from '@/lib/api-client';
import toast from 'react-hot-toast';
import { useQueryClient } from '@tanstack/react-query';

export default function PlaylistsPage() {
  const { data: playlists, isLoading } = usePlaylists();
  const qc = useQueryClient();

  const setAsInbox = async (playlistId: string) => {
    try {
      await apiPost(`/api/playlists/${playlistId}/inbox`);
      await qc.invalidateQueries({ queryKey: ['playlists'] });
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
      <h1 className="text-2xl font-bold">Playlists</h1>
      {playlists.map((pl) => (
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
              onClick={() => setAsInbox(pl.youTubeId)}
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
