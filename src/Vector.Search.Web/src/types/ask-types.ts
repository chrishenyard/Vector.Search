export interface AskRequest {
  question: string;
  topK: number;
}

export interface CodeChunk {
  id: string;
  filename: string;
  language: string;
  content: string;
  hash: string;
  embedding?: number[] | null;
}

export interface SearchResponse {
  chunk: CodeChunk;
  score?: number | null;
}

export interface AskResponse {
  answer: string;
  searchResonses: SearchResponse[];
}
