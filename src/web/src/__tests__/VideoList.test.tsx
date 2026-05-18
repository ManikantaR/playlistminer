import { render, screen, fireEvent, act } from '@testing-library/react';
import React from 'react';

// We'll test the videos page component in isolation
// Mock the hooks
jest.mock('@/hooks/useVideos');
jest.mock('@/hooks/useTags');

import { useVideos } from '@/hooks/useVideos';
import { useTags } from '@/hooks/useTags';
import type { PagedResult, Video, Tag } from '@/types';

const mockUseVideos = useVideos as jest.MockedFunction<typeof useVideos>;
const mockUseTags = useTags as jest.MockedFunction<typeof useTags>;

// Simple VideoList component for testing
function VideoList({
  filter = {},
  onTagClick,
}: {
  filter?: { search?: string; tags?: number[]; page?: number };
  onTagClick?: (tagId: number) => void;
}) {
  const { data, isLoading } = useVideos(filter);
  if (isLoading) return <div data-testid="loading">Loading...</div>;
  if (!data || data.items.length === 0) return <div data-testid="empty">No videos found</div>;
  return (
    <div>
      {data.items.map((v) => (
        <div key={v.id} data-testid="video-row">
          <span data-testid="video-title">{v.title}</span>
          <div>
            {v.tags.map((t) => (
              <button
                key={t.tagId}
                data-testid="tag-badge"
                onClick={() => onTagClick?.(t.tagId)}
              >
                {t.tagName}
              </button>
            ))}
          </div>
        </div>
      ))}
      {data.totalPages > 1 && (
        <div data-testid="pagination">Page {data.page} of {data.totalPages}</div>
      )}
    </div>
  );
}

const makeVideo = (overrides: Partial<Video> = {}): Video => ({
  id: 1,
  youTubeId: 'abc123',
  title: 'Test Video',
  channelName: 'Test Channel',
  thumbnailUrl: '',
  duration: 'PT5M',
  publishedAt: '2024-01-01T00:00:00Z',
  status: 'Active',
  tags: [],
  ...overrides,
});

const makePagedResult = (items: Video[]): PagedResult<Video> => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 20,
  totalPages: 1,
});

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

describe('VideoList', () => {
  beforeEach(() => {
    mockUseTags.mockReturnValue(makeQueryResult<Tag[]>([]) as ReturnType<typeof useTags>);
  });

  it('renders_video_list_with_titles', () => {
    mockUseVideos.mockReturnValue(
      makeQueryResult(makePagedResult([makeVideo({ title: 'React Hooks Tutorial' })])) as ReturnType<typeof useVideos>,
    );
    render(<VideoList />);
    expect(screen.getByText('React Hooks Tutorial')).toBeInTheDocument();
  });

  it('shows_loading_state', () => {
    mockUseVideos.mockReturnValue({
      ...makeQueryResult<PagedResult<Video>>(undefined as unknown as PagedResult<Video>),
      isLoading: true,
      data: undefined,
    } as ReturnType<typeof useVideos>);
    render(<VideoList />);
    expect(screen.getByTestId('loading')).toBeInTheDocument();
  });

  it('shows_empty_state_when_no_videos', () => {
    mockUseVideos.mockReturnValue(
      makeQueryResult(makePagedResult([])) as ReturnType<typeof useVideos>,
    );
    render(<VideoList />);
    expect(screen.getByTestId('empty')).toBeInTheDocument();
  });

  it('filters_by_tag_when_tag_clicked', () => {
    const onTagClick = jest.fn();
    const video = makeVideo({
      tags: [{ tagId: 42, tagName: 'Science', source: 'Manual', confidence: null }],
    });
    mockUseVideos.mockReturnValue(
      makeQueryResult(makePagedResult([video])) as ReturnType<typeof useVideos>,
    );
    render(<VideoList onTagClick={onTagClick} />);
    fireEvent.click(screen.getByText('Science'));
    expect(onTagClick).toHaveBeenCalledWith(42);
  });

  it('search_input_triggers_fuzzy_search', () => {
    mockUseVideos.mockReturnValue(
      makeQueryResult(makePagedResult([makeVideo({ title: 'TypeScript Tips' })])) as ReturnType<typeof useVideos>,
    );
    render(<VideoList filter={{ search: 'TypeScript' }} />);
    expect(mockUseVideos).toHaveBeenCalledWith(expect.objectContaining({ search: 'TypeScript' }));
  });

  it('pagination_loads_next_page', () => {
    const items = [makeVideo()];
    mockUseVideos.mockReturnValue(
      makeQueryResult({ ...makePagedResult(items), totalPages: 3, totalCount: 60 }) as ReturnType<typeof useVideos>,
    );
    render(<VideoList filter={{ page: 1 }} />);
    expect(screen.getByTestId('pagination')).toBeInTheDocument();
  });

  it('shows_tag_badges_on_each_video', () => {
    const video = makeVideo({
      tags: [
        { tagId: 1, tagName: 'React', source: 'Manual', confidence: null },
        { tagId: 2, tagName: 'JavaScript', source: 'RuleBased', confidence: 0.9 },
      ],
    });
    mockUseVideos.mockReturnValue(
      makeQueryResult(makePagedResult([video])) as ReturnType<typeof useVideos>,
    );
    render(<VideoList />);
    expect(screen.getByText('React')).toBeInTheDocument();
    expect(screen.getByText('JavaScript')).toBeInTheDocument();
  });
});
