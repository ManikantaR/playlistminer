export interface Video {
  id: number;
  youTubeId: string;
  title: string;
  channelName: string;
  thumbnailUrl: string;
  duration: string;
  publishedAt: string;
  status: 'Active' | 'Unavailable' | 'Private' | 'Deleted' | 'Archived';
  tags: TagSuggestion[];
}

export interface VideoDetail extends Video {
  description: string;
  channelId: string;
  playlists: string[];
}

export interface TagSuggestion {
  tagId: number;
  tagName: string;
  source: 'Manual' | 'RuleBased' | 'TfIdf' | 'Ollama' | 'Suggested';
  confidence: number | null;
}

export interface Tag {
  id: number;
  name: string;
  slug: string;
  category: string | null;
  videoCount: number;
}

export interface TagRule {
  id: number;
  tagId: number;
  keyword: string;
  field: 'Title' | 'Description' | 'Both';
  weight: number;
  isLearned: boolean;
}

export interface Playlist {
  id: number;
  youTubeId: string;
  name: string;
  description: string | null;
  isInbox: boolean;
  itemCount: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface SyncLog {
  id: number;
  syncType: string;
  startedAt: string;
  completedAt: string | null;
  videosProcessed: number;
  status: string;
  errors: string | null;
}

export interface UndoLog {
  id: number;
  videoId: number;
  videoTitle: string;
  sourcePlaylistId: number;
  sourcePlaylistName: string;
  targetPlaylistId: number;
  targetPlaylistName: string;
  createdAt: string;
  expiresAt: string;
  isUndone: boolean;
}

export interface ImportBatch {
  id: number;
  status: string;
  createdAt: string;
}

export interface ImportResult {
  videosFound: number;
  videosImported: number;
  skipped: number;
  errors: number;
  batchId: string;
}
