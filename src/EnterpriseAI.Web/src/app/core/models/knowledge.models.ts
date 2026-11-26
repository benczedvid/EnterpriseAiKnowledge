export interface AskKnowledgeRequest {
  question: string;
}

export interface AskKnowledgeResponse {
  question: string;
  answer: string;
  answeredFromKnowledgeBase: boolean;
  sources: KnowledgeSource[];
}

export interface KnowledgeSource {
  documentId: string;
  chunkId: string;
  sourceId: string;
  documentTitle: string;
  sourceUrl: string;
  sectionTitle: string | null;
  pageNumber: number | null;
  chunkIndex: number;
  similarity: number;
}