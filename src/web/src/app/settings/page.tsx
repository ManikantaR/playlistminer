'use client';
import { useState, Suspense } from 'react';
import { useSyncStatus } from '@/hooks/useSync';
import { useOAuthStatus, useConnect, useDisconnect } from '@/hooks/useOAuth';
import { usePlaylists, useSetInboxPlaylist } from '@/hooks/usePlaylists';
import Card from '@/components/ui/Card';
import { useSearchParams } from 'next/navigation';
import toast from 'react-hot-toast';

function OAuthAlerts() {
  const searchParams = useSearchParams();
  const justConnected = searchParams.get('connected') === 'true';
  const oauthError = searchParams.get('error');

  if (justConnected) {
    return (
      <div className="bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg p-3 text-sm text-green-700 dark:text-green-300">
        YouTube connected successfully!
      </div>
    );
  }

  if (oauthError) {
    return (
      <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-3 text-sm text-red-700 dark:text-red-300">
        {oauthError === 'denied'
          ? 'YouTube authorization was denied.'
          : 'Failed to connect YouTube. Please try again.'}
      </div>
    );
  }

  return null;
}

export default function SettingsPage() {
  const { data: syncStatus } = useSyncStatus();
  const { data: oauthStatus, isLoading: oauthLoading } = useOAuthStatus();
  const { data: playlists, isLoading: playlistsLoading } = usePlaylists();
  const setInboxMutation = useSetInboxPlaylist();
  const connect = useConnect();
  const disconnect = useDisconnect();
  const [tfIdfThreshold, setTfIdfThreshold] = useState(0.3);
  const [ollamaThreshold, setOllamaThreshold] = useState(0.5);
  const [autoAcceptThreshold, setAutoAcceptThreshold] = useState(0.9);
  const [selectedInboxId, setSelectedInboxId] = useState<string | null>(null);

  const connected = oauthStatus?.connected ?? false;
  const currentInbox = playlists?.find((playlist) => playlist.isInbox) ?? null;
  const inboxLikePlaylists = playlists?.filter((playlist) => (
    playlist.name.toLowerCase().includes('inbox')
  )) ?? [];
  const suggestedInbox = inboxLikePlaylists.find((playlist) => !playlist.isInbox) ?? null;
  const defaultPlaylist = currentInbox
    ? playlists?.find((playlist) => !playlist.isInbox) ?? currentInbox
    : suggestedInbox ?? playlists?.[0] ?? null;
  const defaultInboxId = playlists && playlists.length > 0
    ? String(defaultPlaylist?.id ?? '')
    : '';
  const playlistSelectionStillExists = playlists?.some((playlist) => String(playlist.id) === selectedInboxId) ?? false;
  const effectiveSelectedInboxId = selectedInboxId && playlistSelectionStillExists
    ? selectedInboxId
    : defaultInboxId;

  const handleSetInbox = async (playlistId?: number) => {
    const targetPlaylistId = playlistId ?? Number(effectiveSelectedInboxId);

    if (!targetPlaylistId) {
      return;
    }

    try {
      await setInboxMutation.mutateAsync(targetPlaylistId);
      toast.success('Inbox playlist updated');
    } catch {
      toast.error('Failed to set inbox');
    }
  };

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <h1 className="text-2xl font-bold">Settings</h1>

      <Suspense>
        <OAuthAlerts />
      </Suspense>

      <Card>
        <h2 className="text-lg font-semibold mb-3">YouTube Connection</h2>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div
              className={`w-3 h-3 rounded-full ${
                connected ? 'bg-green-500' : 'bg-gray-300 dark:bg-gray-600'
              }`}
            />
            <span className="text-sm">
              {oauthLoading ? 'Checking...' : connected ? 'Connected' : 'Not connected'}
            </span>
          </div>
          {connected ? (
            <button
              onClick={() => disconnect.mutate()}
              disabled={disconnect.isPending}
              className="px-4 py-2 text-sm border border-red-300 dark:border-red-700 text-red-600 dark:text-red-400 rounded-lg hover:bg-red-50 dark:hover:bg-red-900/20 disabled:opacity-50"
            >
              {disconnect.isPending ? 'Disconnecting...' : 'Disconnect'}
            </button>
          ) : (
            <button
              onClick={() => connect.mutate()}
              disabled={connect.isPending || oauthLoading}
              className="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
            >
              {connect.isPending ? 'Redirecting...' : 'Connect YouTube'}
            </button>
          )}
        </div>
        {syncStatus?.lastSync && (
          <p className="text-sm text-gray-500 mt-3">
            Last sync: {new Date(syncStatus.lastSync).toLocaleString()}
          </p>
        )}
      </Card>

      <Card>
        <h2 className="text-lg font-semibold mb-3">Incoming Playlist</h2>
        <div className="space-y-4">
          <div>
            <p className="text-sm text-gray-500 mb-1">Current inbox</p>
            <p className="font-medium">{currentInbox?.name ?? 'No inbox selected yet'}</p>
          </div>

          {playlistsLoading ? (
            <p className="text-sm text-gray-500">Loading playlists...</p>
          ) : !playlists || playlists.length === 0 ? (
            <p className="text-sm text-gray-500">Sync your playlists first to choose an incoming playlist.</p>
          ) : (
            <>
              {inboxLikePlaylists.length > 0 && (
                <div className="rounded-lg border border-blue-200 bg-blue-50 p-3 dark:border-blue-900 dark:bg-blue-950/30">
                  <p className="text-sm font-medium text-blue-900 dark:text-blue-100">
                    Suggested incoming playlists
                  </p>
                  <div className="mt-3 space-y-2">
                    {inboxLikePlaylists.map((playlist) => (
                      <div key={playlist.id} className="flex items-center justify-between gap-3">
                        <div>
                          <p className="text-sm font-medium">{playlist.name}</p>
                          <p className="text-xs text-gray-500">{playlist.itemCount} videos</p>
                        </div>
                        {!playlist.isInbox && (
                          <button
                            type="button"
                            onClick={() => handleSetInbox(playlist.id)}
                            disabled={setInboxMutation.isPending}
                            aria-label={`Set ${playlist.name} as incoming`}
                            className="rounded-lg border border-blue-300 px-3 py-1.5 text-sm font-medium text-blue-700 hover:bg-blue-100 disabled:opacity-50 dark:border-blue-800 dark:text-blue-200 dark:hover:bg-blue-900/40"
                          >
                            Set
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}

              <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                <label className="flex-1 text-sm font-medium text-gray-700 dark:text-gray-200">
                  Incoming playlist
                  <select
                    value={effectiveSelectedInboxId}
                    onChange={(e) => setSelectedInboxId(e.target.value)}
                    className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
                    aria-label="Incoming playlist"
                  >
                    {playlists.map((playlist) => (
                      <option key={playlist.id} value={playlist.id}>
                        {playlist.name}
                      </option>
                    ))}
                  </select>
                </label>
                <button
                  type="button"
                  onClick={() => handleSetInbox()}
                  disabled={!effectiveSelectedInboxId || setInboxMutation.isPending}
                  className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                >
                  {setInboxMutation.isPending ? 'Saving...' : 'Set as Incoming'}
                </button>
              </div>
            </>
          )}
        </div>
      </Card>

      <Card>
        <h2 className="text-lg font-semibold mb-3">Sync Schedule</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          Syncs run automatically via the background worker. Configure the schedule in
          your <code className="bg-gray-100 dark:bg-gray-700 px-1 rounded">appsettings.json</code>.
        </p>
      </Card>

      <Card>
        <h2 className="text-lg font-semibold mb-4">Confidence Thresholds</h2>
        <div className="space-y-5">
          <div>
            <label className="flex justify-between text-sm mb-1">
              <span>TF-IDF Threshold</span>
              <span className="font-medium">{tfIdfThreshold.toFixed(2)}</span>
            </label>
            <input
              type="range"
              min={0}
              max={1}
              step={0.05}
              value={tfIdfThreshold}
              onChange={(e) => setTfIdfThreshold(Number(e.target.value))}
              className="w-full"
              aria-label="TF-IDF threshold"
            />
          </div>
          <div>
            <label className="flex justify-between text-sm mb-1">
              <span>Ollama Threshold</span>
              <span className="font-medium">{ollamaThreshold.toFixed(2)}</span>
            </label>
            <input
              type="range"
              min={0}
              max={1}
              step={0.05}
              value={ollamaThreshold}
              onChange={(e) => setOllamaThreshold(Number(e.target.value))}
              className="w-full"
              aria-label="Ollama threshold"
            />
          </div>
          <div>
            <label className="flex justify-between text-sm mb-1">
              <span>Auto-Accept Threshold</span>
              <span className="font-medium">{autoAcceptThreshold.toFixed(2)}</span>
            </label>
            <input
              type="range"
              min={0}
              max={1}
              step={0.05}
              value={autoAcceptThreshold}
              onChange={(e) => setAutoAcceptThreshold(Number(e.target.value))}
              className="w-full"
              aria-label="Auto-accept threshold"
            />
          </div>
        </div>
      </Card>
    </div>
  );
}
