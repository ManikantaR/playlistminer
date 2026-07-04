'use client';

import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { useBuildOrganizePlan } from '@/hooks/useOrganize';

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

  const buildPlan = async () => {
    await organizePlan.mutateAsync();
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
