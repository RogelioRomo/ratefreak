"use client";
import { useQuery } from "@tanstack/react-query";
import type { LibraryFilters } from "../interfaces/personal";
import { getLibrary, getTracking, getRating, getOwnReview, getAggregate, getReviews } from "../api/personal";
export const personalKeys = {
  library: (account: string, filters?: LibraryFilters | null) => filters ? ["personal", account, "library", filters] as const : ["personal", account, "library"] as const,
  tracking: (account: string, id: string) => ["personal", account, "tracking", id] as const,
  rating: (account: string, id: string) => ["personal", account, "rating", id] as const,
  review: (account: string, id: string) => ["personal", account, "review", id] as const,
  aggregate: (id: string) => ["community", id, "aggregate"] as const,
  reviews: (id: string) => ["community", id, "reviews"] as const,
};
export const useLibrary = (account: string, filters: LibraryFilters | null) => useQuery({ queryKey: [...personalKeys.library(account), filters], queryFn: ({ signal }) => getLibrary(filters!, signal), enabled: !!account && !!filters, retry: false });
export const useTracking = (account: string, id: string, active = true) => useQuery({ queryKey: personalKeys.tracking(account, id), queryFn: ({ signal }) => getTracking(id, signal), enabled: !!account && active, retry: false });
export const useRating = (account: string, id: string, active = true) => useQuery({ queryKey: personalKeys.rating(account, id), queryFn: ({ signal }) => getRating(id, signal), enabled: !!account && active, retry: false });
export const useOwnReview = (account: string, id: string, active = true) => useQuery({ queryKey: personalKeys.review(account, id), queryFn: ({ signal }) => getOwnReview(id, signal), enabled: !!account && active, retry: false });
export const useAggregate = (id: string) => useQuery({ queryKey: personalKeys.aggregate(id), queryFn: ({ signal }) => getAggregate(id, signal), retry: false });
export const useReviews = (id: string, page: number | null) => useQuery({ queryKey: [...personalKeys.reviews(id), page], queryFn: ({ signal }) => getReviews(id, page!, signal), enabled: page !== null, retry: false });
