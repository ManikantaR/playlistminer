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

export interface DuplicatePlaylist {
  playlistId: number;
  playlistName: string;
  isManaged: boolean;
  topic: string | null;
}

export interface DuplicateReview {
  videoId: number;
  youTubeId: string;
  title: string;
  thumbnailUrl: string;
  playlistCount: number;
  playlists: DuplicatePlaylist[];
}

export interface RemoteDuplicateRemovalTarget {
  playlistId: number;
  playlistName: string;
  playlistItemId: string | null;
}

export interface RemoteDuplicateCleanupItem {
  videoId: number;
  youTubeId: string;
  title: string;
  winnerPlaylistId: number;
  winnerPlaylistName: string;
  hasUnresolvedRemovals: boolean;
  loserPlaylists: RemoteDuplicateRemovalTarget[];
}

export interface RemoteDuplicateCleanupResult {
  videosExamined: number;
  removalsPlanned: number;
  removalsExecuted: number;
  removalsSkipped: number;
  deferredCount: number;
  errors: string[];
  runId: string | null;
}

export interface OrganizePlanItem {
  action: 'create_playlist' | 'move' | 'review' | 'no_op';
  videoId: number | null;
  youTubeId: string | null;
  title: string | null;
  sourcePlaylistName: string | null;
  targetPlaylistName: string | null;
  targetPlaylistId: number | null;
  topic: string | null;
  confidence: number | null;
  estimatedQuotaCost: number;
  reason: string;
}

export interface OrganizePlan {
  videosExamined: number;
  totalActions: number;
  totalEstimatedQuotaCost: number;
  items: OrganizePlanItem[];
}

export interface OrganizeExecutionResult {
  videosExamined: number;
  movesPlanned: number;
  movesExecuted: number;
  movesSkipped: number;
  deferredCount: number;
  errors: string[];
  runId: string | null;
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

export interface PipelineRun {
  runId: string;
  pipelineType: string;
  status: string;
  phase: string;
  startedAt: string;
  updatedAt: string;
  completedAt: string | null;
  currentMessage: string | null;
  error: string | null;

  // Sync counters
  playlistsDiscovered: number;
  playlistsProcessed: number;
  playlistItemsFetched: number;
  uniqueVideoIdsIdentified: number;
  videoMetadataBatchesTotal: number;
  videoMetadataBatchesCompleted: number;
  videosUpserted: number;
  playlistVideoLinksWritten: number;
  videosArchived: number;
  videosDeferred: number;
  errorsCount: number;

  // Categorization counters
  videosPendingTagging: number;
  videosProcessed: number;
  videosTagged: number;
  videosSkipped: number;
  ruleBasedHits: number;
  tfidfHits: number;
  ollamaHits: number;
  isStalled?: boolean;
}

export interface PipelineEvent {
  id: number;
  runId: string;
  occurredAt: string;
  level: string;
  phase: string;
  message: string;
  payloadJson: string | null;
}

export interface DependencyHealth {
  database: string;
  oauthConnected: boolean;
  youtubeQuotaAvailable: boolean;
  ollamaReachable: boolean;
  workerStatus: string;
  workerLastHeartbeat: string | null;
}

export interface OperationsHealth {
  apiHealthy: boolean;
  dbHealthy: boolean;
  workerHealthy: boolean;
  workerHeartbeatAgeSeconds: number;
  oauthConnected: boolean;
  quotaExhausted: boolean;
  ollamaReachable: boolean;
  activeRunStalled: boolean;
  activeRunPhase: string | null;
}

export interface OperationsActivityItem {
  id: number;
  runId: string;
  pipelineType: string;
  pipelineLabel: string;
  status: string;
  level: string;
  phase: string;
  message: string;
  occurredAt: string;
}

export interface OperationsActivityFeed {
  items: OperationsActivityItem[];
  limit: number;
  offset: number;
  totalCount: number;
  hasMore: boolean;
}

export interface OperationsQuota {
  movesUsedToday: number;
  moveBudget: number;
  resetsAt: string;
  unitsRemaining: number;
  isBlocked: boolean;
  message: string;
}
