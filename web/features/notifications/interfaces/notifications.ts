export interface NotificationItem {
  id: string;
  kind: "follow" | "comment";
  actor: { id: string; handle: string; displayName: string; avatarUrl?: string | null };
  commentId: string | null;
  reviewId: string | null;
  mediaItemId: string | null;
  createdAt: string;
  readAt: string | null;
}
export interface NotificationPage {
  items: NotificationItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  unreadCount: number;
}
