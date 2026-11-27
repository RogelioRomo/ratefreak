'use client';
import Link from 'next/link';
import { useEffect } from 'react';
import { LoadingState } from '@/components/ui/loading-state';
import { ErrorState } from '@/components/ui/error-state';
import { MediaArtwork } from '@/features/catalog/components/media-artwork';
import { mediaLabels } from '@/features/catalog/query/filters';
import { useSession } from '@/features/auth/hooks/use-session';
import { ApiError } from '@/shared/api/api-error';
import { useRecentRatings } from '../hooks/use-recent-ratings';
import styles from './recently-rated.module.css';
export function RecentlyRated({ accountId }: { accountId: string }) {
  const recent = useRecentRatings(accountId);
  const { refresh } = useSession();
  const unauthorized = recent.error instanceof ApiError && recent.error.status === 401;
  useEffect(() => { if (unauthorized) void refresh().catch(() => {}); }, [unauthorized, refresh]);
  return <section className={styles.section} aria-labelledby="recently-rated-title">
    <header className={styles.heading}><h2 id="recently-rated-title">Recently rated</h2><p>Your latest opinions, all in one place.</p></header>
    {recent.isError ? <ErrorState title="Recently rated unavailable." description="We couldn’t load your latest ratings." onRetry={() => void recent.refetch()} />
      : recent.isPending ? <LoadingState label="Loading your recent ratings…" />
      : recent.data.items.length ? <ul className={styles.grid}>{recent.data.items.map(({ media, score }) => <li key={media.id}>
        <Link href={`/catalog/${media.id}`} className={styles.item}>
          <div className={styles.cover}><MediaArtwork reference={media.imageReference} title={media.title} type={media.type} sizes="(max-width: 36rem) 40vw, (max-width: 60rem) 22vw, 160px" /></div>
          <h3>{media.title}</h3>
        </Link>
        <p className={styles.type}>{mediaLabels[media.type]}</p><p className={styles.score}>Your rating: {score.toFixed(1)} / 5</p>
      </li>)}</ul>
      : <div className={styles.empty}><p>Rate something to start your collection of opinions.</p><Link href="/catalog">Browse the catalog</Link></div>}
  </section>;
}
