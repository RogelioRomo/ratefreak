import type { MediaSummary } from "@/features/catalog/interfaces/media-summary";
export type TrackingStatus = "planned" | "in_progress" | "completed" | "paused" | "dropped";
export interface TrackingInput { status: TrackingStatus; startedAt: string | null; completedAt: string | null; }
export interface LibraryItem extends TrackingInput { media: MediaSummary; createdAt: string; updatedAt: string; }
export interface PersonalPage<T> { items: T[]; page: number; pageSize: number; totalCount: number; }
export interface LibraryFilters { status: TrackingStatus | ""; type: string; page: number; pageSize: number; }
export interface RatingDetails { id: string; mediaId: string; score: number; createdAt: string; updatedAt: string; }
export interface RatingAggregate { count: number; average: number | null; }
export interface ReviewInput { body: string; isSpoiler: boolean; }
export interface ReviewAuthor { id: string; handle: string; displayName: string; }
export interface ReviewDetails extends ReviewInput { id: string; mediaId: string; author: ReviewAuthor; createdAt: string; updatedAt: string; }
