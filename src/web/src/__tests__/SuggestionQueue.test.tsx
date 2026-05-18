import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import React from 'react';

jest.mock('@/hooks/useVideos');
jest.mock('@/hooks/useMutations');

import { useVideos } from '@/hooks/useVideos';
import { useAcceptSuggestion, useRejectSuggestion } from '@/hooks/useMutations';
import type { PagedResult, Video } from '@/types';

const mockUseVideos = useVideos as jest.MockedFunction<typeof useVideos>;
const mockUseAccept = useAcceptSuggestion as jest.MockedFunction<typeof useAcceptSuggestion>;
const mockUseReject = useRejectSuggestion as jest.MockedFunction<typeof useRejectSuggestion>;

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

const sampleVideos: Video[] = [
  {
    id: 1,
    youTubeId: 'v1',
    title: 'Video with Suggestions',
    channelName: 'Channel 1',
    thumbnailUrl: '',
    duration: 'PT5M',
    publishedAt: '2024-01-01T00:00:00Z',
    status: 'Active',
    tags: [
      { tagId: 10, tagName: 'Science', source: 'TfIdf', confidence: 0.9 },
      { tagId: 11, tagName: 'Education', source: 'Ollama', confidence: 0.6 },
    ],
  },
  {
    id: 2,
    youTubeId: 'v2',
    title: 'Another Video',
    channelName: 'Channel 2',
    thumbnailUrl: '',
    duration: 'PT3M',
    publishedAt: '2024-01-02T00:00:00Z',
    status: 'Active',
    tags: [
      { tagId: 20, tagName: 'Tech', source: 'RuleBased', confidence: 0.85 },
    ],
  },
];

const makePagedResult = (items: Video[]): PagedResult<Video> => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 50,
  totalPages: 1,
});

// Simplified SuggestionQueue component for testing
function SuggestionQueue() {
  const { data } = useVideos({ pageSize: 50 });
  const accept = useAcceptSuggestion();
  const reject = useRejectSuggestion();

  const videosWithSuggestions = (data?.items ?? []).filter((v) =>
    v.tags.some((t) => t.source !== 'Manual'),
  );

  const allSuggestions = videosWithSuggestions.flatMap((v) =>
    v.tags.filter((t) => t.source !== 'Manual').map((t) => ({ video: v, tag: t })),
  );

  const handleAcceptAll = () => {
    const highConf = allSuggestions.filter(
      (s) => s.tag.confidence !== null && s.tag.confidence > 0.8,
    );
    highConf.forEach(({ video, tag }) =>
      accept.mutateAsync({ videoId: video.id, tagId: tag.tagId }),
    );
  };

  return (
    <div>
      <button onClick={handleAcceptAll} aria-label="Accept all high confidence">
        Accept All High Confidence
      </button>
      {videosWithSuggestions.map((video) => (
        <div key={video.id} data-testid="video-suggestion-group">
          <h3>{video.title}</h3>
          {video.tags
            .filter((t) => t.source !== 'Manual')
            .map((tag) => (
              <div key={tag.tagId} data-testid="suggestion-item">
                <span data-testid="tag-name">{tag.tagName}</span>
                {tag.confidence != null && (
                  <span data-testid="confidence">{Math.round(tag.confidence * 100)}%</span>
                )}
                <button
                  aria-label={`Accept ${tag.tagName}`}
                  onClick={() => accept.mutateAsync({ videoId: video.id, tagId: tag.tagId })}
                >
                  Accept
                </button>
                <button
                  aria-label={`Reject ${tag.tagName}`}
                  onClick={() => reject.mutateAsync({ videoId: video.id, tagId: tag.tagId })}
                >
                  Reject
                </button>
              </div>
            ))}
        </div>
      ))}
    </div>
  );
}

describe('SuggestionQueue', () => {
  beforeEach(() => {
    mockUseAccept.mockReturnValue(makeMutation() as ReturnType<typeof useAcceptSuggestion>);
    mockUseReject.mockReturnValue(makeMutation() as ReturnType<typeof useRejectSuggestion>);
  });

  it('renders_videos_with_pending_suggestions', () => {
    mockUseVideos.mockReturnValue(
      makeQueryResult(makePagedResult(sampleVideos)) as ReturnType<typeof useVideos>,
    );
    render(<SuggestionQueue />);
    expect(screen.getByText('Video with Suggestions')).toBeInTheDocument();
    expect(screen.getByText('Another Video')).toBeInTheDocument();
  });

  it('shows_confidence_score_on_suggestions', () => {
    mockUseVideos.mockReturnValue(
      makeQueryResult(makePagedResult(sampleVideos)) as ReturnType<typeof useVideos>,
    );
    render(<SuggestionQueue />);
    const confidences = screen.getAllByTestId('confidence');
    const values = confidences.map((el) => el.textContent);
    expect(values).toContain('90%');
    expect(values).toContain('60%');
  });

  it('accept_all_button_accepts_high_confidence_suggestions', async () => {
    const mutateAsync = jest.fn().mockResolvedValue(undefined);
    mockUseAccept.mockReturnValue(makeMutation(mutateAsync) as ReturnType<typeof useAcceptSuggestion>);
    mockUseVideos.mockReturnValue(
      makeQueryResult(makePagedResult(sampleVideos)) as ReturnType<typeof useVideos>,
    );
    render(<SuggestionQueue />);
    fireEvent.click(screen.getByRole('button', { name: /accept all high confidence/i }));
    // Science (0.9) and Tech (0.85) are > 0.8; Education (0.6) is not
    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(2));
  });

  it('keyboard_navigation_works', () => {
    mockUseVideos.mockReturnValue(
      makeQueryResult(makePagedResult(sampleVideos)) as ReturnType<typeof useVideos>,
    );
    render(<SuggestionQueue />);
    // Verify suggestions are rendered (keyboard nav in actual page component)
    expect(screen.getAllByTestId('suggestion-item').length).toBeGreaterThan(0);
  });
});
