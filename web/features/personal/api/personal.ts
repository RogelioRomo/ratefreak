import { apiRequest } from "@/shared/api/request";
import { ApiError } from "@/shared/api/api-error";
import type { LibraryFilters, LibraryItem, TrackingInput, RatingDetails, RatingAggregate, ReviewInput, ReviewDetails, PersonalPage } from "../interfaces/personal";
import { libraryParams } from "../query/filters";
export async function ownRecord<T>(path: string, signal?: AbortSignal): Promise<T | null> {
  try { return await apiRequest<T>(path, { signal }); }
  catch (error) { if (error instanceof ApiError && error.status === 404) return null; throw error; }
}
export const getLibrary = (filters: LibraryFilters, signal?: AbortSignal) => apiRequest<PersonalPage<LibraryItem>>(`users/me/library?${libraryParams(filters, true)}`, { signal });
export const getTracking = (id: string, signal?: AbortSignal) => ownRecord<LibraryItem>(`users/me/library/${id}`, signal);
export const getRating = (id: string, signal?: AbortSignal) => ownRecord<RatingDetails>(`media/${id}/rating`, signal);
export const getOwnReview = (id: string, signal?: AbortSignal) => ownRecord<ReviewDetails>(`media/${id}/review`, signal);
export const getAggregate = (id: string, signal?: AbortSignal) => apiRequest<RatingAggregate>(`media/${id}/ratings`, { signal });
export const getReviews = (id: string, page: number, signal?: AbortSignal) => apiRequest<PersonalPage<ReviewDetails>>(`media/${id}/reviews?page=${page}&pageSize=20`, { signal });
export const saveTracking = (id: string, body: TrackingInput, signal?: AbortSignal) => apiRequest<LibraryItem>(`users/me/library/${id}`, { method: "PUT", body, signal });
export const saveRating = (id: string, score: number, signal?: AbortSignal) => apiRequest<RatingDetails>(`media/${id}/rating`, { method: "PUT", body: { score }, signal });
export const saveReview = (id: string, body: ReviewInput, reviewId?: string, signal?: AbortSignal) => apiRequest<ReviewDetails>(reviewId ? `reviews/${reviewId}` : `media/${id}/reviews`, { method: reviewId ? "PATCH" : "POST", body, signal });
export const deletePersonal = (path: string, signal?: AbortSignal) => apiRequest<void>(path, { method: "DELETE", signal });
