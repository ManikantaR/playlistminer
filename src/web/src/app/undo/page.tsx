'use client';
import { useUndoLogs } from '@/hooks/useUndo';
import { useUndoAction } from '@/hooks/useMutations';
import EmptyState from '@/components/ui/EmptyState';
import Button from '@/components/ui/Button';
import toast from 'react-hot-toast';

export default function UndoPage() {
  const { data: logs, isLoading } = useUndoLogs();
  const undoAction = useUndoAction();

  const handleUndo = async (id: number) => {
    try {
      await undoAction.mutateAsync(id);
      toast.success('Action undone');
    } catch {
      toast.error('Failed to undo');
    }
  };

  if (isLoading) return <div className="animate-pulse text-gray-500 p-8">Loading…</div>;
  if (!logs || logs.length === 0) {
    return <EmptyState title="No undo history" message="No recent actions to undo." />;
  }

  return (
    <div className="max-w-4xl mx-auto space-y-4">
      <h1 className="text-2xl font-bold">Undo History</h1>
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 dark:bg-gray-700">
            <tr>
              <th className="py-3 px-4 text-left font-medium text-gray-600 dark:text-gray-300">Video</th>
              <th className="py-3 px-4 text-left font-medium text-gray-600 dark:text-gray-300">From</th>
              <th className="py-3 px-4 text-left font-medium text-gray-600 dark:text-gray-300">To</th>
              <th className="py-3 px-4 text-left font-medium text-gray-600 dark:text-gray-300">Created</th>
              <th className="py-3 px-4 text-left font-medium text-gray-600 dark:text-gray-300">Expires</th>
              <th className="py-3 px-4" />
            </tr>
          </thead>
          <tbody>
            {logs.map((log) => {
              const expired = new Date(log.expiresAt) < new Date();
              return (
                <tr
                  key={log.id}
                  className={`border-t border-gray-100 dark:border-gray-700 ${
                    expired || log.isUndone ? 'opacity-40' : ''
                  }`}
                >
                  <td className="py-3 px-4 max-w-xs">
                    <span className="line-clamp-1">{log.videoTitle}</span>
                  </td>
                  <td className="py-3 px-4 text-gray-600 dark:text-gray-400">{log.sourcePlaylistName}</td>
                  <td className="py-3 px-4 text-gray-600 dark:text-gray-400">{log.targetPlaylistName}</td>
                  <td className="py-3 px-4 text-gray-500 whitespace-nowrap">
                    {new Date(log.createdAt).toLocaleString()}
                  </td>
                  <td className="py-3 px-4 text-gray-500 whitespace-nowrap">
                    {new Date(log.expiresAt).toLocaleString()}
                  </td>
                  <td className="py-3 px-4">
                    {!log.isUndone && !expired && (
                      <Button size="sm" variant="secondary" onClick={() => handleUndo(log.id)}>
                        Undo
                      </Button>
                    )}
                    {log.isUndone && <span className="text-xs text-gray-400">Undone</span>}
                    {expired && !log.isUndone && <span className="text-xs text-red-400">Expired</span>}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
