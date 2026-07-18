'use client';
import { useState, Suspense } from 'react';
import { useSyncStatus } from '@/hooks/useSync';
import { useOAuthStatus, useConnect, useDisconnect } from '@/hooks/useOAuth';
import { usePlaylists, useSetInboxPlaylist } from '@/hooks/usePlaylists';
import { useAutomationPolicy, useUpdateAutomationPolicy } from '@/hooks/useAutomationPolicy';
import { useOperationsQuota } from '@/hooks/useOperations';
import { usePipelineHistory } from '@/hooks/usePipeline';
import Card from '@/components/ui/Card';
import { useSearchParams } from 'next/navigation';
import toast from 'react-hot-toast';
import type { AutomationPolicy } from '@/types';

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
  const { data: automationPolicy, isLoading: automationPolicyLoading } = useAutomationPolicy();
  const { data: operationsQuota } = useOperationsQuota();
  const { data: pipelineHistory } = usePipelineHistory();
  const setInboxMutation = useSetInboxPlaylist();
  const updateAutomationPolicy = useUpdateAutomationPolicy();
  const connect = useConnect();
  const disconnect = useDisconnect();
  const [tfIdfThreshold, setTfIdfThreshold] = useState(0.3);
  const [ollamaThreshold, setOllamaThreshold] = useState(0.5);
  const [autoAcceptThreshold, setAutoAcceptThreshold] = useState(0.9);
  const [selectedInboxId, setSelectedInboxId] = useState<string | null>(null);
  const [policyDraft, setPolicyDraft] = useState<AutomationPolicy | null>(null);
  const effectivePolicyDraft = policyDraft ?? automationPolicy ?? null;
  const lastAutomationRun = pipelineHistory?.find((run) => (
    run.pipelineType === 'organize-execute'
    || run.pipelineType === 'organize-execution'
    || run.pipelineType === 'remote-duplicate-cleanup'
  )) ?? null;

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

  const updatePolicyDraft = <K extends keyof AutomationPolicy>(
    key: K,
    value: AutomationPolicy[K],
  ) => {
    setPolicyDraft((current) => {
      const basePolicy = current ?? automationPolicy;
      return basePolicy ? { ...basePolicy, [key]: value } : current;
    });
  };

  const handleSaveAutomationPolicy = async () => {
    if (!effectivePolicyDraft) {
      return;
    }

    try {
      await updateAutomationPolicy.mutateAsync(effectivePolicyDraft);
      setPolicyDraft(null);
      toast.success('Automation policy updated');
    } catch {
      toast.error('Failed to update automation policy');
    }
  };

  const nextScheduledRun = effectivePolicyDraft
    ? `${effectivePolicyDraft.offPeakWindowStart} off-peak window`
    : 'Not scheduled';

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

      <Card>
        <h2 className="text-lg font-semibold mb-4">Automation Policy</h2>
        {automationPolicyLoading || !effectivePolicyDraft ? (
          <p className="text-sm text-gray-500">Loading automation policy...</p>
        ) : (
          <div className="space-y-5">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="rounded-lg border border-gray-200 p-3 dark:border-gray-700">
                <p className="text-xs font-medium uppercase text-gray-500">Quota remaining</p>
                <p className="mt-1 text-lg font-semibold">
                  {operationsQuota ? `${operationsQuota.unitsRemaining} / ${operationsQuota.moveBudget}` : 'Unknown'}
                </p>
              </div>
              <div className="rounded-lg border border-gray-200 p-3 dark:border-gray-700">
                <p className="text-xs font-medium uppercase text-gray-500">Pending approvals</p>
                <p className="mt-1 text-lg font-semibold">0</p>
              </div>
              <div className="rounded-lg border border-gray-200 p-3 dark:border-gray-700">
                <p className="text-xs font-medium uppercase text-gray-500">Last automation run</p>
                <p className="mt-1 text-sm font-semibold">
                  {lastAutomationRun
                    ? `${lastAutomationRun.status} · ${new Date(lastAutomationRun.updatedAt).toLocaleString()}`
                    : 'None yet'}
                </p>
              </div>
              <div className="rounded-lg border border-gray-200 p-3 dark:border-gray-700">
                <p className="text-xs font-medium uppercase text-gray-500">Next scheduled run</p>
                <p className="mt-1 text-sm font-semibold">{nextScheduledRun}</p>
              </div>
            </div>

            <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
              Automation mode
              <select
                value={effectivePolicyDraft.mode}
                onChange={(event) => updatePolicyDraft('mode', event.target.value as AutomationPolicy['mode'])}
                aria-label="Automation mode"
                className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
              >
                <option value="manual">Manual</option>
                <option value="first_week_approval">First-week approval</option>
                <option value="aggressive_with_undo">Aggressive with undo</option>
              </select>
            </label>

            <div className="grid gap-4 sm:grid-cols-2">
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                High confidence threshold
                <input
                  type="number"
                  min={0}
                  max={1}
                  step={0.01}
                  value={effectivePolicyDraft.highConfidenceThreshold}
                  onChange={(event) => updatePolicyDraft('highConfidenceThreshold', Number(event.target.value))}
                  aria-label="High confidence threshold"
                  className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
                />
              </label>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                Review threshold
                <input
                  type="number"
                  min={0}
                  max={1}
                  step={0.01}
                  value={effectivePolicyDraft.reviewThreshold}
                  onChange={(event) => updatePolicyDraft('reviewThreshold', Number(event.target.value))}
                  aria-label="Review threshold"
                  className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
                />
              </label>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                Daily move budget
                <input
                  type="number"
                  min={0}
                  max={500}
                  value={effectivePolicyDraft.dailyMoveBudget}
                  onChange={(event) => updatePolicyDraft('dailyMoveBudget', Number(event.target.value))}
                  aria-label="Daily move budget"
                  className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
                />
              </label>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                Nightly restore budget
                <input
                  type="number"
                  min={0}
                  max={500}
                  value={effectivePolicyDraft.nightlyRestoreBudget}
                  onChange={(event) => updatePolicyDraft('nightlyRestoreBudget', Number(event.target.value))}
                  aria-label="Nightly restore budget"
                  className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
                />
              </label>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                Cleanup recommendations
                <input
                  type="number"
                  min={1}
                  max={25}
                  value={effectivePolicyDraft.cleanupRecommendationCount}
                  onChange={(event) => updatePolicyDraft('cleanupRecommendationCount', Number(event.target.value))}
                  aria-label="Cleanup recommendations"
                  className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
                />
              </label>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                Off-peak start
                <input
                  type="time"
                  value={effectivePolicyDraft.offPeakWindowStart}
                  onChange={(event) => updatePolicyDraft('offPeakWindowStart', event.target.value)}
                  aria-label="Off-peak start"
                  className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
                />
              </label>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                Off-peak end
                <input
                  type="time"
                  value={effectivePolicyDraft.offPeakWindowEnd}
                  onChange={(event) => updatePolicyDraft('offPeakWindowEnd', event.target.value)}
                  aria-label="Off-peak end"
                  className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
                />
              </label>
            </div>

            <div className="space-y-3">
              <label className="flex items-center gap-3 text-sm font-medium text-gray-700 dark:text-gray-200">
                <input
                  type="checkbox"
                  checked={effectivePolicyDraft.publicAiFallbackEnabled}
                  onChange={(event) => updatePolicyDraft('publicAiFallbackEnabled', event.target.checked)}
                  aria-label="Enable public AI fallback"
                  className="h-4 w-4 rounded border-gray-300"
                />
                Enable public AI fallback
              </label>
              <div className="grid gap-4 sm:grid-cols-2">
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                  Public AI provider
                  <select
                    value={effectivePolicyDraft.publicAiProvider ?? ''}
                    onChange={(event) => updatePolicyDraft('publicAiProvider', event.target.value || null)}
                    disabled={!effectivePolicyDraft.publicAiFallbackEnabled}
                    aria-label="Public AI provider"
                    className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm disabled:opacity-60 dark:border-gray-700 dark:bg-gray-900"
                  >
                    <option value="">None</option>
                    <option value="openai">OpenAI</option>
                    <option value="gemini">Gemini</option>
                  </select>
                </label>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                  Public AI model
                  <input
                    type="text"
                    value={effectivePolicyDraft.publicAiModel ?? ''}
                    onChange={(event) => updatePolicyDraft('publicAiModel', event.target.value || null)}
                    disabled={!effectivePolicyDraft.publicAiFallbackEnabled}
                    aria-label="Public AI model"
                    className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm disabled:opacity-60 dark:border-gray-700 dark:bg-gray-900"
                  />
                </label>
              </div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                Transcript cloud policy
                <select
                  value={effectivePolicyDraft.transcriptCloudPolicy}
                  onChange={(event) => updatePolicyDraft('transcriptCloudPolicy', event.target.value as AutomationPolicy['transcriptCloudPolicy'])}
                  aria-label="Transcript cloud policy"
                  className="mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
                >
                  <option value="never">Never send transcripts</option>
                  <option value="metadata_only">Metadata only</option>
                  <option value="allow_transcripts">Allow transcripts</option>
                </select>
              </label>
            </div>

            <div className="flex flex-col gap-3 border-t border-gray-200 pt-4 dark:border-gray-700 sm:flex-row sm:items-center sm:justify-between">
              <label className="flex items-center gap-3 text-sm font-medium text-gray-700 dark:text-gray-200">
                <input
                  type="checkbox"
                  checked={effectivePolicyDraft.isPaused}
                  onChange={(event) => updatePolicyDraft('isPaused', event.target.checked)}
                  aria-label="Pause automation"
                  className="h-4 w-4 rounded border-gray-300"
                />
                Pause automation
              </label>
              <button
                type="button"
                onClick={handleSaveAutomationPolicy}
                disabled={updateAutomationPolicy.isPending}
                className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {updateAutomationPolicy.isPending ? 'Saving...' : 'Save Automation Policy'}
              </button>
            </div>
          </div>
        )}
      </Card>
    </div>
  );
}
