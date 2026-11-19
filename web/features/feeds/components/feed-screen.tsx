'use client';
import Link from 'next/link';
import { useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQueries, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/ui/loading-state';
import { ErrorState } from '@/components/ui/error-state';
import { useSession } from '@/features/auth/hooks/use-session';
import { ApiError } from '@/shared/api/api-error';
import { useFeed } from '../hooks/use-feed';
import { getFeed } from '../api/feed';
import { feedKeys, resetFeeds } from '../query/keys';
import { readFeedFilters, feedHref } from '../query/filters';
import type { FeedFilters } from '../interfaces/feed';
import { FeedEntryCard } from './feed-entry';
import styles from './feed.module.css';
export function FeedScreen({accountId}:{accountId:string}) {
  const search=useSearchParams();const filters=readFeedFilters(new URLSearchParams(search.toString()));
  if(!filters)return <section className={styles.screen}><h1>Your feed</h1><p role="alert">Invalid feed filters.</p><Link href="/">Reset feed</Link></section>;
  return <FeedView key={`${accountId}:${filters.tab}:${filters.type}`} accountId={accountId} filters={filters}/>;
}
function FeedView({accountId,filters}:{accountId:string;filters:FeedFilters}) {
  const router=useRouter();const client=useQueryClient();const session=useSession();
  const [navigation,setNavigation]=useState<{cursor:string|null;prior:(string|null)[];target?:string}>({cursor:filters.cursor,prior:[]});
  if(navigation.cursor!==filters.cursor)setNavigation({cursor:filters.cursor,prior:navigation.target===filters.cursor?[...navigation.prior,navigation.cursor]:[]});
  const current=useFeed(accountId,filters,true);
  const previous=useQueries({queries:navigation.prior.map(cursor=>({queryKey:feedKeys.page(accountId,filters.tab,filters.type,cursor),queryFn:({signal}:{signal:AbortSignal})=>getFeed({...filters,cursor},signal),staleTime:30_000,retry:false,refetchOnWindowFocus:'always' as const}))});
  const queries=[...previous,current];const error=queries.find(query=>query.isError)?.error;
  const {refresh:refreshSession}=session;
  useEffect(()=>{if(error instanceof ApiError && error.status===401)void refreshSession().catch(()=>{});},[error,refreshSession]);
  const loading=queries.some(query=>query.isPending);
  const entries=[...new Map(queries.flatMap(query=>query.data?.items??[]).map(entry=>[`${entry.author.id}:${entry.media.id}`,entry])).values()];
  const refresh=()=>{setNavigation({cursor:null,prior:[]});if(filters.cursor)router.push(feedHref({...filters,cursor:null}));void resetFeeds(client);};
  return <section className={styles.screen} aria-labelledby="feed-title">
    <header className={styles.heading}><p className={styles.eyebrow}>A place for your interests</p><h1 id="feed-title">Good taste travels.</h1><p>Reviews and ratings from your corner of culture.</p></header>
    <div className={styles.toolbar}><nav className={styles.tabs} aria-label="Feed"><Link href={feedHref({...filters,tab:'following',cursor:null})} aria-current={filters.tab==='following'?'page':undefined}>Following</Link><Link href={feedHref({...filters,tab:'community',cursor:null})} aria-current={filters.tab==='community'?'page':undefined}>Community</Link></nav>
      <div className={styles.filters}><label>Media type<select value={filters.type} onChange={event=>router.push(feedHref({...filters,type:event.target.value as FeedFilters['type'],cursor:null}))}><option value="">All media</option><option value="movie">Movies</option><option value="tv_show">Television</option><option value="album">Music</option><option value="video_game">Games</option><option value="book">Books</option><option value="custom">Custom</option></select></label><Button variant="secondary" disabled={queries.some(query=>query.isFetching)} onClick={refresh}>Refresh</Button></div>
    </div>
    <p className={styles.context}>{filters.tab==='following'?'Public opinions from people you follow.':'Public opinions from across this instance.'}</p>
    {error ? <ErrorState title="Feed unavailable." description="We couldn’t refresh this feed. Please try again." onRetry={()=>{void resetFeeds(client);}}/> : loading ? <LoadingState label="Loading feed…"/> : entries.length ? <><ul className={styles.list}>{entries.map(entry=><li key={`${entry.author.id}:${entry.media.id}`}><FeedEntryCard entry={entry}/></li>)}</ul><div className={styles.pagination}>{current.data?.nextCursor ? <Button variant="secondary" onClick={()=>{const next=current.data!.nextCursor!;setNavigation({...navigation,target:next});router.push(feedHref({...filters,cursor:next}));}}>Load more</Button> : <p>You’re all caught up.</p>}</div></> : <div className={styles.empty}><h2>{filters.tab==='following'&&!current.data?.hasFollowees?'Find your people.':'No activity here yet.'}</h2><p>{filters.tab==='following'?'Follow reviewers whose taste interests you. Their public reviews and shared ratings will appear here.':filters.type?'Try another media type, or share an opinion of your own.':'Be the first to write a review or share a rating.'}</p>{filters.tab==='following'?<Link href={feedHref({tab:'community',type:'',cursor:null})}>Explore Community</Link>:<Link href="/catalog">Browse the catalog</Link>}</div>}
  </section>;
}
