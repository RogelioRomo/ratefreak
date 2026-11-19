'use client';
import { useQuery } from '@tanstack/react-query';
import { useSession } from '@/features/auth/hooks/use-session';
import { getFeed } from '../api/feed';
import { feedKeys } from '../query/keys';
import type { FeedFilters } from '../interfaces/feed';
export function useFeed(accountId: string, filters: FeedFilters, active: boolean) {
  const session=useSession();
  const result=useQuery({queryKey:feedKeys.page(accountId,filters.tab,filters.type,filters.cursor),queryFn:({signal})=>getFeed(filters,signal),enabled:active && session.account?.id===accountId && !session.error && !session.isTransitioning,staleTime:30_000,retry:false,refetchOnWindowFocus:'always',refetchOnMount:'always'});
  return result;
}
