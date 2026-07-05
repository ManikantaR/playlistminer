'use client';

import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { useBuildOrganizePlan, useExecuteOrganize } from '@/hooks/useOrganize';

function formatActionLabel(action: string, targetPlaylistName: string | null) {
  switch (action) {
    case 'create_playlist':
      return 'Create playlist';
    case 'move':
      return `Move to ${targetPlaylistName}`;
    case 'review':
      return 'Needs review';
    default:
      return action;
  }
}

export default function OrganizePage() {
  const organizePlan = useBuildOrganizePlan();
  const organizeExecution = useExecuteOrganize();

  const buildPlan = async () => {
    await organizePlan.mutateAsync();
  };

  const executeBatch = async () => {
    await organizeExecution.mutateAsync();
  };

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Organize Planner</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            Dry-run preview for draining the incoming playlist into managed topic playlists.
          </p>
        </div>
        <Button onClick={buildPlan} disabled={organizePlan.isPending}>
          {organizePlan.isPending ? 'Planning...' : 'Build Organize Plan'}
        </Button>
      </div>

      <Card>
        <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
          <div>
            <p className="text-sm font-semibold text-gray-900 dark:text-gray-100">Execute Organize Batch</p>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
              Runs the next small organize batch against the current plan and records an operations run.
            </p>
          </div>
          <Button onClick={executeBatch} disabled={organizeExecution.isPending}>
            {organizeExecution.isPending ? 'Executing...' : 'Execute Organize Batch'}
          </Button>
        </div>
      </Card>

      {organizeExecution.data && (
        <Card>
          <div className="flex flex-col gap-3">
            <div>
              <p className="text-sm font-semibold text-gray-900 dark:text-gray-100">Last execution</p>
              <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
                Run ID {organizeExecution.data.runId ?? 'n/a'}
              </p>
            </div>
            <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500 dark:text-gray-400">Examined</p>
                <p className="mt-1 text-2xl font-bold">{organizeExecution.data.videosExamined}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500 dark:text-gray-400">Planned</p>
                <p className="mt-1 text-2xl font-bold">{organizeExecution.data.movesPlanned}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500 dark:text-gray-400">Executed</p>
                <p className="mt-1 text-2xl font-bold">{organizeExecution.data.movesExecuted}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-gray-500 dark:text-gray-400">Deferred</p>
                <p className="mt-1 text-2xl font-bold">{organizeExecution.data.deferredCount}</p>
              </div>
            </div>
            {organizeExecution.data.errors.length > 0 && (
              <div className="rounded-lg border border-orange-200 bg-orange-50 px-3 py-2 text-sm text-orange-800 dark:border-orange-900/40 dark:bg-orange-950/20 dark:text-orange-300">
                {organizeExecution.data.errors.join(' ')}
              </div>
            )}
          </div>
        </Card>
      )}

      {!organizePlan.data ? (
        <Card>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            No organize plan built yet.
          </p>
        </Card>
      ) : (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <Card>
              <p className="text-sm text-gray-500 dark:text-gray-400">Videos examined</p>
              <p className="text-3xl font-bold mt-1">{organizePlan.data.videosExamined}</p>
            </Card>
            <Card>
              <p className="text-sm text-gray-500 dark:text-gray-400">Planned actions</p>
              <p className="text-3xl font-bold mt-1">{organizePlan.data.totalActions}</p>
            </Card>
            <Card>
              <p className="text-sm text-gray-500 dark:text-gray-400">Estimated quota cost</p>
              <p className="text-3xl font-bold mt-1">{organizePlan.data.totalEstimatedQuotaCost}</p>
            </Card>
          </div>

          <div className="space-y-3">
            {organizePlan.data.items.map((item, index) => (
              <Card key={`${item.action}-${item.videoId ?? 'playlist'}-${index}`}>
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="font-semibold text-gray-900 dark:text-gray-100">
                      {formatActionLabel(item.action, item.targetPlaylistName)}
                    </p>
                    {item.title && (
                      <p className="mt-1 text-sm text-gray-700 dark:text-gray-300">{item.title}</p>
                    )}
                    {item.topic && (
                      <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
                        Topic: {item.topic}
                      </p>
                    )}
                    <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">{item.reason}</p>
                  </div>
                  <div className="text-right text-sm text-gray-500 dark:text-gray-400">
                    <p>{item.estimatedQuotaCost} quota</p>
                    {item.confidence !== null && (
                      <p className="mt-1">Confidence {(item.confidence * 100).toFixed(0)}%</p>
                    )}
                  </div>
                </div>
              </Card>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
