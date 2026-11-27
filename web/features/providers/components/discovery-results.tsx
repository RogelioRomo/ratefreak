'use client';
import Link from 'next/link';
import { useCallback, useEffect, useState } from 'react';
import { IconArrowLeft, IconArrowRight } from '@tabler/icons-react';
import { MediaArtwork, supportedArtwork } from '@/features/catalog/components/media-artwork';
import { MusicMetadata } from '@/features/catalog/components/music-metadata';
import { mediaLabels } from '@/features/catalog/query/filters';
import type { DiscoveryResultProps, DiscoveryResultsProps } from '../interfaces/component-props';
import { discoveryHref, musicArtworkOnly } from '../query/filters';
import { previewHref, providerIdentity } from '../query/identity';
import styles from './providers.module.css';

export function DiscoveryResults({ data, filters }: DiscoveryResultsProps) {
  const onlyArtwork = musicArtworkOnly(filters);
  const [availability, setAvailability] = useState<Record<string, boolean>>({});
  const resolved = useCallback((id: string, available: boolean) => {
    setAvailability(previous => Object.hasOwn(previous, id) ? previous : { ...previous, [id]: available });
  }, []);
  const rows = data.items.flatMap(item => {
    const identity = providerIdentity(item.provider, item.type, item.externalId);
    return identity ? [{ item, identity, id: `${item.provider}:${item.type}:${item.externalId}:${item.imageReference ?? ''}`, supported: !!supportedArtwork(item.imageReference) }] : [];
  });
  const pending = onlyArtwork ? rows.filter(row => row.supported && availability[row.id] === undefined) : [];
  const active = new Set(pending.slice(0, 4).map(row => row.id));
  const count = onlyArtwork ? rows.filter(row => availability[row.id] === true).length : rows.length;
  return <section aria-label="Provider results">
    <div className={styles.summary}><p role="status">{count} {count === 1 ? 'result' : 'results'}{onlyArtwork ? ' with artwork' : ''} on this page{pending.length > 0 ? ' · Checking covers…' : ''}</p><p>Page {filters.page}</p></div>
    {!rows.length ? <div className={styles.guidance}><h2>No titles on this page.</h2><p>Try another search{filters.page > 1 ? ' or return to the first page.' : '.'}</p>{filters.page > 1 && <Link className={styles.link} href={discoveryHref({ ...filters, page: 1 })}>Go to first page</Link>}</div>
      : onlyArtwork && !count && !pending.length ? <div className={styles.guidance}><h2>No covers loaded on this page.</h2><p>These results may have no artwork, or their covers couldn’t be loaded. Try the next page or show every result.</p><Link className={styles.link} href={discoveryHref({ ...filters, artworkOnly: false })}>Show all results</Link></div> : null}
    <ul className={styles.grid}>{rows.map(row => {
      if (onlyArtwork && (!row.supported || availability[row.id] === false || (availability[row.id] === undefined && !active.has(row.id)))) return null;
      return <DiscoveryResult key={row.id} item={row.item} identity={row.identity} id={row.id} checking={onlyArtwork && active.has(row.id)} hidden={onlyArtwork && availability[row.id] !== true} onResolved={resolved} />;
    })}</ul>
    <nav className={styles.pagination} aria-label="Provider pages">{filters.page > 1 ? <Link href={discoveryHref({ ...filters, page: filters.page - 1 })}><IconArrowLeft size={18} aria-hidden="true" />Previous</Link> : <span>Previous</span>}<span>Page {filters.page}</span>{data.items.length > 0 && filters.page < 500 ? <Link href={discoveryHref({ ...filters, page: filters.page + 1 })}>Next<IconArrowRight size={18} aria-hidden="true" /></Link> : <span>Next</span>}</nav>
  </section>;
}

function DiscoveryResult({ item, identity, id, checking, hidden, onResolved }: DiscoveryResultProps) {
  useEffect(() => {
    if (!checking) return;
    const timeout = setTimeout(() => onResolved(id, false), 8000);
    return () => clearTimeout(timeout);
  }, [checking, id, onResolved]);
  return <li hidden={hidden}><Link className={styles.entry} href={previewHref(identity)} aria-labelledby={`external-${item.externalId}`}>
    <MediaArtwork reference={item.imageReference} title={item.title} type={item.type} loading={checking ? 'eager' : 'lazy'} onLoad={() => onResolved(id, true)} onUnavailable={() => onResolved(id, false)} />
    <h2 id={`external-${item.externalId}`}>{item.title}</h2>
  </Link>{(item.type === 'song' || item.type === 'album') && <MusicMetadata metadata={item.metadata} compact />}<p className={styles.meta}>{mediaLabels[item.type]}{item.releaseDate ? ` · ${item.releaseDate.slice(0, 4)}` : ''}</p></li>;
}
