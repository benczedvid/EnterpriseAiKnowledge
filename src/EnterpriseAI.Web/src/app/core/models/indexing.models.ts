export interface IndexingSummary {
  documents: DocumentIndexingSummary;
  chunks: ChunkIndexingSummary;
  latestRun: LatestIndexingRunSummary | null;
}

export interface DocumentIndexingSummary {
  total: number;
  indexed: number;
  processing: number;
  failed: number;
  requiresOcr: number;
  deleted: number;
}

export interface ChunkIndexingSummary {
  total: number;
}

export interface LatestIndexingRunSummary {
  id: string;
  status: string;
  startedAt: string;
  completedAt: string | null;
  durationSeconds: number | null;
  processedCount: number;
  indexedCount: number;
  skippedUnchangedCount: number;
  failedCount: number;
}

export interface IndexingRunsResponse {
  count: number;
  runs: IndexingRun[];
}

export interface IndexingRun {
  id: string;
  sourceType: string;
  folderPath: string | null;
  requestedMaximumDocuments: number;
  forceReindex: boolean;
  status: string;
  startedAt: string;
  completedAt: string | null;
  durationSeconds: number | null;
  discoveredCount: number;
  processedCount: number;
  indexedCount: number;
  skippedUnchangedCount: number;
  skippedUnsupportedCount: number;
  requiresOcrCount: number;
  failedCount: number;
  notFoundCount: number;
  totalChunkCount: number;
  failureMessage: string | null;
}

export interface IndexingRunItem {
  id: string;
  sourceId: string;
  fileName: string | null;
  status: string;
  message: string;
  documentId: string | null;
  chunkCount: number;
  characterCount: number;
  indexedAt: string | null;
  createdAt: string;
}

export interface IndexingRunDetails extends IndexingRun {
  items: IndexingRunItem[];
}

export interface StartIndexingBatchRequest {
  folderPath: string | null;
  maximumDocuments: number;
  forceReindex: boolean;
}

export interface StartIndexingBatchResponse {
  indexingRunId: string;
  status: 'Queued';
  statusUrl: string;
}


export interface StartIndexingBatchItemResponse {
  sourceId: string;
  fileName: string | null;
  status: string;
  message: string;
  documentId: string | null;
  chunkCount: number;
  characterCount: number;
  indexedAt: string | null;
}