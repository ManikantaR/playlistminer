import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';

jest.mock('@/hooks/useOrganize');

import { useBuildOrganizePlan, useExecuteOrganize } from '@/hooks/useOrganize';
import OrganizePage from '../app/organize/page';
import type { OrganizeExecutionResult, OrganizePlan } from '@/types';

const mockUseBuildOrganizePlan = useBuildOrganizePlan as jest.MockedFunction<typeof useBuildOrganizePlan>;
const mockUseExecuteOrganize = useExecuteOrganize as jest.MockedFunction<typeof useExecuteOrganize>;

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
    mockUseExecuteOrganize.mockReturnValue({
      mutateAsync: jest.fn(),
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useExecuteOrganize>);
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
});
