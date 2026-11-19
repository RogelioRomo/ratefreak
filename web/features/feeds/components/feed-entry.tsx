'use client';
import Link from 'next/link';
import { useState } from 'react';
import { IconStar } from '@tabler/icons-react';
import { MediaArtwork } from '@/features/catalog/components/media-artwork';
import { ReportControl } from '@/features/reporting/components/report-control';
import { Button } from '@/components/ui/button';
import type { FeedEntry } from '../interfaces/feed';
import styles from './feed.module.css';
export function FeedEntryCard({entry}:{entry:FeedEntry}) {
  // Remount the text state when the review identity/content changes.
  const review=entry.review;
  return <article className={styles.entry}>
    <Link className={styles.cover} href={`/catalog/${entry.media.id}`} tabIndex={-1} aria-hidden="true"><MediaArtwork reference={entry.media.imageReference} title={entry.media.title} type={entry.media.type} sizes="96px"/></Link>
    <div className={styles.content}>
      <p className={styles.byline}><Link href={`/users/${entry.author.handle}`}>{entry.author.displayName}</Link><span>{review?'reviewed':'rated'}</span></p>
      <h2><Link href={`/catalog/${entry.media.id}`}>{entry.media.title}</Link></h2>
      <div className={styles.metadata}>{entry.score!==null && <span className={styles.score} aria-label={`${entry.score} out of 5 stars`}><IconStar size={16} aria-hidden="true"/>{entry.score.toFixed(1)}<span className={styles.outOf}> / 5</span></span>}<time dateTime={entry.publishedAt}>{new Date(entry.publishedAt).toLocaleDateString('en',{month:'short',day:'numeric',year:'numeric'})}</time></div>
      {review && <ReviewExcerpt key={`${review.id}:${review.excerpt}:${review.isSpoiler}`} excerpt={review.excerpt} spoiler={review.isSpoiler}/>}
      {review && <div className={styles.entryActions}><Link href={`/reviews/${review.id}`}>Read review and comments</Link><ReportControl target={{type:'review',id:review.id,authorId:entry.author.id}} available/></div>}
    </div>
  </article>;
}
function ReviewExcerpt({excerpt,spoiler}:{excerpt:string;spoiler:boolean}) {
  const [revealed,setRevealed]=useState(false);
  return <div className={styles.review}>{spoiler && <Button variant="secondary" aria-expanded={revealed} onClick={()=>setRevealed(!revealed)}>{revealed?'Hide spoilers':'Show spoilers'}</Button>}{(!spoiler || revealed) && <p>{excerpt}</p>}</div>;
}
