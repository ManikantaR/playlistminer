import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';

jest.mock('@/hooks/useOrganize');
jest.mock('@/hooks/usePipeline');

import { useBuildOrganizePlan, useExecuteOrganize, useProcessNow } from '@/hooks/useOrganize';
import { usePipelineHistory } from '@/hooks/usePipeline';
import OrganizePage from '../app/organize/page';
import type { AgentProcessResult, OrganizeExecutionResult, OrganizePlan, PipelineRun } from '@/types';

const mockUseBuildOrganizePlan = useBuildOrganizePlan as jest.MockedFunction<typeof useBuildOrganizePlan>;
const mockUseExecuteOrganize = useExecuteOrganize as jest.MockedFunction<typeof useExecuteOrganize>;
const mockUseProcessNow = useProcessNow as jest.MockedFunction<typeof useProcessNow>;
const mockUsePipelineHistory = usePipelineHistory as jest.MockedFunction<typeof usePipelineHistory>;

const renderPage = () => {
  const queryClient = new QueryClient();

  return render(
    <QueryClientProvider client={queryClient}>
      <OrganizePage />
    </QueryClientProvider>,
  );
};

describe('OrganizePage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockUsePipelineHistory.mockReturnValue({
      data: [],
      isPending: false,
    } as ReturnType<typeof usePipelineHistory>);
    mockUseExecuteOrganize.mockReturnValue({
      mutateAsync: jest.fn(),
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useExecuteOrganize>);
    mockUseProcessNow.mockReturnValue({
      mutateAsync: jest.fn(),
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useProcessNow>);
  });

  it('renders organize plan results after building the plan', () => {
    const samplePlan: OrganizePlan = {
      videosExamined: 2,
      totalActions: 3,
      totalEstimatedQuotaCost: 250,
      items: [
        {
          action: 'create_playlist',
          videoId: null,
          youTubeId: null,
          title: null,
          sourcePlaylistName: null,
          targetPlaylistName: 'AI Agents',
          targetPlaylistId: null,
          topic: 'AI Agents',
          confidence: null,
          estimatedQuotaCost: 50,
          reason: 'Managed playlist does not exist yet.',
        },
        {
          action: 'move',
          videoId: 42,
          youTubeId: 'dupvideo01',
          title: 'Distributed Systems Deep Dive',
          sourcePlaylistName: 'Incoming',
          targetPlaylistName: 'AI Agents',
          targetPlaylistId: 7,
          topic: 'AI Agents',
          confidence: 0.92,
          estimatedQuotaCost: 100,
          reason: 'Best topic confidence is above threshold.',
        },
        {
          action: 'review',
          videoId: 99,
          youTubeId: 'review001',
          title: 'Unclear tutorial',
          sourcePlaylistName: 'Incoming',
          targetPlaylistName: null,
          targetPlaylistId: null,
          topic: null,
          confidence: 0.41,
          estimatedQuotaCost: 0,
          reason: 'Best available topic confidence is below threshold.',
        },
      ],
    };

    mockUseBuildOrganizePlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: samplePlan,
      isPending: false,
    } as ReturnType<typeof useBuildOrganizePlan>);

    renderPage();

    expect(screen.getByText('Organize Planner')).toBeInTheDocument();
    expect(screen.getByText('Videos examined')).toBeInTheDocument();
    expect(screen.getByText('250')).toBeInTheDocument();
    expect(screen.getByText('Create playlist')).toBeInTheDocument();
    expect(screen.getByText('Move to AI Agents')).toBeInTheDocument();
    expect(screen.getByText('Needs review')).toBeInTheDocument();
  });

  it('renders organize execution summary after executing a batch', () => {
    const sampleExecution: OrganizeExecutionResult = {
      videosExamined: 3,
      movesPlanned: 2,
      movesExecuted: 2,
      movesSkipped: 0,
      deferredCount: 1,
      errors: [],
      runId: 'run-123',
    };

    mockUseBuildOrganizePlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useBuildOrganizePlan>);
    mockUseExecuteOrganize.mockReturnValue({
      mutateAsync: jest.fn(),
      data: sampleExecution,
      isPending: false,
    } as ReturnType<typeof useExecuteOrganize>);

    renderPage();

    expect(screen.getByText('Last execution')).toBeInTheDocument();
    expect(screen.getByText('Executed')).toBeInTheDocument();
    expect(screen.getByText(/Run ID/)).toBeInTheDocument();
  });

  it('triggers plan building from the page action', () => {
    const mutateAsync = jest.fn();

    mockUseBuildOrganizePlan.mockReturnValue({
      mutateAsync,
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useBuildOrganizePlan>);

    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'Build Organize Plan' }));
    expect(mutateAsync).toHaveBeenCalled();
  });

  it('triggers organize execution from the page action', () => {
    const mutateAsync = jest.fn();

    mockUseBuildOrganizePlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useBuildOrganizePlan>);
    mockUseExecuteOrganize.mockReturnValue({
      mutateAsync,
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useExecuteOrganize>);

    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'Execute Organize Batch' }));
    expect(mutateAsync).toHaveBeenCalled();
  });

  it('triggers process now and shows skipped reachability result', () => {
    const mutateAsync = jest.fn();
    const processResult: AgentProcessResult = {
      status: 'skipped',
      message: 'Ollama is unavailable. Incoming videos were left queued.',
      sync: null,
      execution: null,
    };

    mockUseBuildOrganizePlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useBuildOrganizePlan>);
    mockUseProcessNow.mockReturnValue({
      mutateAsync,
      data: processResult,
      isPending: false,
    } as ReturnType<typeof useProcessNow>);

    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'Process Now' }));
    expect(mutateAsync).toHaveBeenCalled();
    expect(screen.getByText('Process now result')).toBeInTheDocument();
    expect(screen.getByText(/Ollama is unavailable/)).toBeInTheDocument();
  });

  it('renders manual intervention guidance when the latest organize run failed irrecoverably', () => {
    const failedRun: PipelineRun = {
      runId: 'organize-run-999',
      pipelineType: 'organize-execute',
      status: 'failed',
      phase: 'manual_intervention_required',
      startedAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      completedAt: new Date().toISOString(),
      currentMessage: 'Run failed: Manual cleanup is required.',
      error: 'Move of video 1 partially succeeded on YouTube and rollback failed. Manual cleanup is required.',
      playlistsDiscovered: 0,
      playlistsProcessed: 0,
      playlistItemsFetched: 0,
      uniqueVideoIdsIdentified: 0,
      videoMetadataBatchesTotal: 0,
      videoMetadataBatchesCompleted: 0,
      videosUpserted: 0,
      playlistVideoLinksWritten: 0,
      videosArchived: 0,
      videosDeferred: 1,
      errorsCount: 1,
      videosPendingTagging: 1,
      videosProcessed: 0,
      videosTagged: 0,
      videosSkipped: 0,
      ruleBasedHits: 0,
      tfidfHits: 0,
      ollamaHits: 0,
    };

    mockUseBuildOrganizePlan.mockReturnValue({
      mutateAsync: jest.fn(),
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useBuildOrganizePlan>);
    mockUsePipelineHistory.mockReturnValue({
      data: [failedRun],
      isPending: false,
    } as ReturnType<typeof usePipelineHistory>);

    renderPage();

    expect(screen.getByText('Manual Cleanup Required')).toBeInTheDocument();
    expect(screen.getByText(/organize-run-999/)).toBeInTheDocument();
    expect(screen.getByText(/review the run details on the operations page before executing another batch/i)).toBeInTheDocument();
  });
});
