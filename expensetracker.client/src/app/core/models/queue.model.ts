export interface QueuedItem {
  id: string;
  preview: string;
  createdAt: string;
}

export interface RecentItem {
  id: string;
  preview: string;
  status: string;
  failureReason: string | null;
  processedAt: string;
}

export interface QueueStatus {
  pendingCount: number;
  pending: QueuedItem[];
  recentlyProcessed: RecentItem[];
}
