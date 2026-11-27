import type { MediaSummary } from '@/features/catalog/interfaces/media-summary';
export interface RecentRating { media: MediaSummary; score: number; updatedAt: string; }
export interface RecentRatingsPage { items: RecentRating[]; }
