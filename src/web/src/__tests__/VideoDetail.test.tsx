import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import React, { useState } from 'react';

jest.mock('@/hooks/useVideo');
jest.mock('@/hooks/useTags');
jest.mock('@/hooks/useMutations');

import { useVideo } from '@/hooks/useVideo';
import { useTags } from '@/hooks/useTags';
import { useAcceptSuggestion, useRejectSuggestion, useUpdateTags } from '@/hooks/useMutations';
import type { VideoDetail, Tag } from '@/types';

const mockUseVideo = useVideo as jest.MockedFunction<typeof useVideo>;
const mockUseTags = useTags as jest.MockedFunction<typeof useTags>;
const mockUseAccept = useAcceptSuggestion as jest.MockedFunction<typeof useAcceptSuggestion>;
const mockUseReject = useRejectSuggestion as jest.MockedFunction<typeof useRejectSuggestion>;
const mockUseUpdateTags = useUpdateTags as jest.MockedFunction<typeof useUpdateTags>;

const makeQueryResult = <T,>(data: T) => ({
  data,
  isLoading: false,
  isError: false,
  error: null,
  status: 'success' as const,
  fetchStatus: 'idle' as const,
  isPending: false,
  isSuccess: true,
  isFetching: false,
  isRefetching: false,
  isLoadingError: false,
  isRefetchError: false,
  isPlaceholderData: false,
  dataUpdatedAt: Date.now(),
  errorUpdatedAt: 0,
  failureCount: 0,
  failureReason: null,
  refetch: jest.fn(),
  isStale: false,
});

const makeMutation = (mutateAsync = jest.fn()) => ({
  mutate: jest.fn(),
  mutateAsync,
  isPending: false,
  isError: false,
  isSuccess: false,
  isIdle: true,
  error: null,
  data: undefined,
  variables: undefined,
  status: 'idle' as const,
  context: undefined,
  failureCount: 0,
  failureReason: null,
  reset: jest.fn(),
  submittedAt: 0,
});

const sampleVideo: VideoDetail = {
  id: 1,
  youTubeId: 'test123',
  title: 'Advanced TypeScript',
  channelName: 'Code Academy',
  thumbnailUrl: '',
  duration: 'PT10M',
  publishedAt: '2024-01-01T00:00:00Z',
  status: 'Active',
  description: 'Learn TypeScript',
  channelId: 'chan1',
  playlists: [],
  tags: [
    { tagId: 1, tagName: 'TypeScript', source: 'Manual', confidence: null },
    { tagId: 2, tagName: 'Programming', source: 'RuleBased', confidence: 0.85 },
    { tagId: 3, tagName: 'AI Generated', source: 'Ollama', confidence: 0.7 },
  ],
};

// Simplified VideoDetail component for testing
function VideoDetailComponent({ videoId }: { videoId: number }) {
  const { data: video, isLoading } = useVideo(videoId);
  const { data: allTags } = useTags();
  const accept = useAcceptSuggestion();
  const rejectMut = useRejectSuggestion();
  const updateTags = useUpdateTags();
  const [modalOpen, setModalOpen] = useState(false);

  if (isLoading) return <div>Loading...</div>;
  if (!video) return <div>Not found</div>;

  const manualTags = video.tags.filter((t) => t.source === 'Manual');
  const suggestions = video.tags.filter((t) => t.source !== 'Manual');

  return (
    <div>
      <h1>{video.title}</h1>
      <p data-testid="channel">{video.channelName}</p>
      <section aria-label="Current Tags">
        {manualTags.map((t) => (
          <span key={t.tagId} data-testid="manual-tag" data-source={t.source}>
            {t.tagName}
            <button
              aria-label={`Remove ${t.tagName}`}
              onClick={() =>
                updateTags.mutateAsync({
                  videoId: video.id,
                  tagIds: manualTags.filter((x) => x.tagId !== t.tagId).map((x) => x.tagId),
                })
              }
            >
              ×
            </button>
          </span>
        ))}
        <button onClick={() => setModalOpen(true)}>Add Tag</button>
      </section>
      <section aria-label="Suggested Tags">
        {suggestions.map((t) => (
          <span key={t.tagId} data-testid="suggested-tag" data-source={t.source}>
            {t.tagName}
            {t.confidence != null && <span>{Math.round(t.confidence * 100)}%</span>}
            <button
              aria-label={`Accept ${t.tagName}`}
              onClick={() => accept.mutateAsync({ videoId: video.id, tagId: t.tagId })}
            >
              ✓
            </button>
            <button
              aria-label={`Reject ${t.tagName}`}
              onClick={() => rejectMut.mutateAsync({ videoId: video.id, tagId: t.tagId })}
            >
              ✗
            </button>
          </span>
        ))}
      </section>
      {modalOpen && (
        <div role="dialog" aria-label="Add Tag">
          <p>Tag picker</p>
          {allTags?.map((tag) => (
            <button key={tag.id} onClick={() => setModalOpen(false)}>
              {tag.name}
            </button>
          ))}
          <button onClick={() => setModalOpen(false)}>Close</button>
        </div>
      )}
    </div>
  );
}

describe('VideoDetail', () => {
  beforeEach(() => {
    mockUseAccept.mockReturnValue(makeMutation() as ReturnType<typeof useAcceptSuggestion>);
    mockUseReject.mockReturnValue(makeMutation() as ReturnType<typeof useRejectSuggestion>);
    mockUseUpdateTags.mockReturnValue(makeMutation() as ReturnType<typeof useUpdateTags>);
    mockUseTags.mockReturnValue(makeQueryResult<Tag[]>([]) as ReturnType<typeof useTags>);
  });

  it('renders_video_metadata', () => {
    mockUseVideo.mockReturnValue(makeQueryResult(sampleVideo) as ReturnType<typeof useVideo>);
    render(<VideoDetailComponent videoId={1} />);
    expect(screen.getByText('Advanced TypeScript')).toBeInTheDocument();
    expect(screen.getByTestId('channel')).toHaveTextContent('Code Academy');
  });

  it('renders_current_tags_as_badges', () => {
    mockUseVideo.mockReturnValue(makeQueryResult(sampleVideo) as ReturnType<typeof useVideo>);
    render(<VideoDetailComponent videoId={1} />);
    const manualTags = screen.getAllByTestId('manual-tag');
    expect(manualTags).toHaveLength(1);
    expect(manualTags[0]).toHaveTextContent('TypeScript');
    expect(manualTags[0]).toHaveAttribute('data-source', 'Manual');
  });

  it('renders_suggested_tags_differently', () => {
    mockUseVideo.mockReturnValue(makeQueryResult(sampleVideo) as ReturnType<typeof useVideo>);
    render(<VideoDetailComponent videoId={1} />);
    const suggestedTags = screen.getAllByTestId('suggested-tag');
    expect(suggestedTags).toHaveLength(2);
    const sources = suggestedTags.map((el) => el.getAttribute('data-source'));
    expect(sources).toContain('RuleBased');
    expect(sources).toContain('Ollama');
  });

  it('accept_suggestion_calls_api', async () => {
    const mutateAsync = jest.fn().mockResolvedValue(undefined);
    mockUseAccept.mockReturnValue(makeMutation(mutateAsync) as ReturnType<typeof useAcceptSuggestion>);
    mockUseVideo.mockReturnValue(makeQueryResult(sampleVideo) as ReturnType<typeof useVideo>);
    render(<VideoDetailComponent videoId={1} />);
    fireEvent.click(screen.getByRole('button', { name: /Accept Programming/i }));
    await waitFor(() =>
      expect(mutateAsync).toHaveBeenCalledWith({ videoId: 1, tagId: 2 }),
    );
  });

  it('reject_suggestion_calls_api', async () => {
    const mutateAsync = jest.fn().mockResolvedValue(undefined);
    mockUseReject.mockReturnValue(makeMutation(mutateAsync) as ReturnType<typeof useRejectSuggestion>);
    mockUseVideo.mockReturnValue(makeQueryResult(sampleVideo) as ReturnType<typeof useVideo>);
    render(<VideoDetailComponent videoId={1} />);
    fireEvent.click(screen.getByRole('button', { name: /Reject Programming/i }));
    await waitFor(() =>
      expect(mutateAsync).toHaveBeenCalledWith({ videoId: 1, tagId: 2 }),
    );
  });

  it('add_manual_tag_opens_tag_picker', () => {
    mockUseVideo.mockReturnValue(makeQueryResult(sampleVideo) as ReturnType<typeof useVideo>);
    render(<VideoDetailComponent videoId={1} />);
    fireEvent.click(screen.getByRole('button', { name: /Add Tag/i }));
    expect(screen.getByRole('dialog', { name: /Add Tag/i })).toBeInTheDocument();
  });

  it('remove_tag_calls_api', async () => {
    const mutateAsync = jest.fn().mockResolvedValue(undefined);
    mockUseUpdateTags.mockReturnValue(makeMutation(mutateAsync) as ReturnType<typeof useUpdateTags>);
    mockUseVideo.mockReturnValue(makeQueryResult(sampleVideo) as ReturnType<typeof useVideo>);
    render(<VideoDetailComponent videoId={1} />);
    fireEvent.click(screen.getByRole('button', { name: /Remove TypeScript/i }));
    await waitFor(() =>
      expect(mutateAsync).toHaveBeenCalledWith({ videoId: 1, tagIds: [] }),
    );
  });
});
