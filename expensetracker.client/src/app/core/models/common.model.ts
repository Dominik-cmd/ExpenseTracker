export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface NarrativeResponse {
  content: string | null;
  generatedAt: string;
  modelUsed: string;
  isStale: boolean;
}
