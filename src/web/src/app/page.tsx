'use client';
import { useSyncHistory, useSyncStatus } from '@/hooks/useSync';
import { useTriggerSync } from '@/hooks/useMutations';
import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Link from 'next/link';
import { RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { useVideos } from '@/hooks/useVideos';
import { useTags } from '@/hooks/useTags';
import PipelineCard from '@/components/PipelineCard';

export default function DashboardPage() {
  const { data: videosData } = useVideos({ pageSize: 1 });
  const { data: tags } = useTags();
  const { data: syncStatus } = useSyncStatus();
  const { data: syncHistory } = useSyncHistory();
  const triggerSync = useTriggerSync();

  const pendingSuggestions = videosData?.totalCount ?? 0;

  const handleSync = async () => {
    try {
      await triggerSync.mutateAsync();
      toast.success('Sync triggered!');
    } catch {
      toast.error('Failed to trigger sync');
    }
  };

  const recentHistory = syncHistory?.slice(0, 5) ?? [];

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Dashboard</h1>
        <Button onClick={handleSync} disabled={triggerSync.isPending}>
          <RefreshCw className={`w-4 h-4 mr-2 ${triggerSync.isPending ? 'animate-spin' : ''}`} />
          Sync Now
        </Button>
      </div>

      <PipelineCard />

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <Card>
          <p className="text-sm text-gray-500 dark:text-gray-400">Total Videos</p>
          <p className="text-3xl font-bold mt-1">{videosData?.totalCount ?? '–'}</p>
        </Card>
        <Card>
          <p className="text-sm text-gray-500 dark:text-gray-400">Total Tags</p>
          <p className="text-3xl font-bold mt-1">{tags?.length ?? '–'}</p>
        </Card>
        <Card>
          <p className="text-sm text-gray-500 dark:text-gray-400">Pending Suggestions</p>
          <p className="text-3xl font-bold mt-1">{pendingSuggestions}</p>
        </Card>
        <Card>
          <p className="text-sm text-gray-500 dark:text-gray-400">Last Sync</p>
          <p className="text-sm font-medium mt-1">
            {syncStatus?.lastSync
              ? new Date(syncStatus.lastSync).toLocaleString()
              : 'Never'}
          </p>
        </Card>
      </div>

      <Card>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold">Recent Sync History</h2>
          <Link href="/suggestions" className="text-sm text-blue-600 hover:underline">
            Review Suggestions →
          </Link>
        </div>
        {recentHistory.length === 0 ? (
          <p className="text-gray-500 text-sm">No sync history yet.</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b dark:border-gray-700 text-left">
                <th className="py-2 pr-4 text-gray-500">Type</th>
                <th className="py-2 pr-4 text-gray-500">Started</th>
                <th className="py-2 pr-4 text-gray-500">Videos</th>
                <th className="py-2 text-gray-500">Status</th>
              </tr>
            </thead>
            <tbody>
              {recentHistory.map((log) => (
                <tr key={log.id} className="border-b dark:border-gray-800">
                  <td className="py-2 pr-4">{log.syncType}</td>
                  <td className="py-2 pr-4">{new Date(log.startedAt).toLocaleString()}</td>
                  <td className="py-2 pr-4">{log.videosProcessed}</td>
                  <td className="py-2">
                    <span
                      className={`px-2 py-0.5 rounded text-xs ${
                        log.status === 'Completed'
                          ? 'bg-green-100 text-green-700'
                          : log.status === 'Failed'
                          ? 'bg-red-100 text-red-700'
                          : 'bg-yellow-100 text-yellow-700'
                      }`}
                    >
                      {log.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  );
}
