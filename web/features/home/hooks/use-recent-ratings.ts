'use client';
import { useQuery } from '@tanstack/react-query';
import { getRecentRatings } from '../api/recent-ratings';
export const recentRatingsKey = (accountId: string) => ['personal', accountId, 'recent-ratings'] as const;
export function useRecentRatings(accountId: string) {
  return useQuery({ queryKey: recentRatingsKey(accountId), queryFn: ({ signal }) => getRecentRatings(signal), retry: false, staleTime: 0, refetchOnWindowFocus: 'always' });
}
