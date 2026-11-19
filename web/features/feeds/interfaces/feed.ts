import type { MediaSummary } from '@/features/catalog/interfaces/media-summary';
import type { MediaType } from '@/features/catalog/interfaces/catalog-filters';
import type { ReviewAuthor } from '@/features/personal/interfaces/personal';
export interface FeedReview { id: string; excerpt: string; isSpoiler: boolean; }
export interface FeedEntry { author: ReviewAuthor; media: MediaSummary; score: number | null; review: FeedReview | null; publishedAt: string; }
export interface FeedPage { items: FeedEntry[]; nextCursor: string | null; hasFollowees: boolean; }
export interface FeedFilters { tab: 'following' | 'community'; type: MediaType | ''; cursor: string | null; }
