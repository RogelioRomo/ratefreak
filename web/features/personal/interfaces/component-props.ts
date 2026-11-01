import type { LibraryItem, LibraryFilters, RatingDetails, ReviewDetails } from "./personal";
export interface MediaActivityProps { mediaId: string; }
export interface PrivateActivityProps extends MediaActivityProps { accountId: string; }
export interface TrackingFormProps extends PrivateActivityProps { saved: LibraryItem | null; }
export interface RatingFormProps extends PrivateActivityProps { saved: RatingDetails | null; }
export interface ReviewFormProps extends PrivateActivityProps { saved: ReviewDetails | null; }
export interface PersonalErrorProps { error: Error | null; }
export interface PersonalReadErrorProps { error: Error; retry: () => void; }

export interface ReviewEntryProps { review: ReviewDetails; }

export interface LibraryContentProps { accountId: string; }
export interface LibraryEmptyProps { filters: LibraryFilters; }

export interface RetainedActivityProps extends PrivateActivityProps { active: boolean; }
