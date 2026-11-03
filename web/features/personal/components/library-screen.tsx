"use client";
import { Select } from "@/components/ui/select";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { IconFilter } from "@tabler/icons-react";
import { useSession } from "@/features/auth/hooks/use-session";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { Button } from "@/components/ui/button";
import { MediaArtwork } from "@/features/catalog/components/media-artwork";
import { mediaTypes, mediaLabels } from "@/features/catalog/query/filters";
import { ApiError } from "@/shared/api/api-error";
import { useLibrary } from "../hooks/use-personal";
import { libraryFilters, libraryHref, trackingStatuses } from "../query/filters";
import type { TrackingStatus } from "../interfaces/personal";
import type { LibraryContentProps, LibraryEmptyProps } from "../interfaces/component-props";
import styles from "./personal.module.css";
export function LibraryScreen() {
  const session = useSession(); const { refresh } = session;
  const [checked, setChecked] = useState(false);
  useEffect(() => { let active = true; void refresh().catch(() => {}).finally(() => { if (active) setChecked(true); }); return () => { active = false; }; }, [refresh]);
  return <div className={styles.screen}><header className={styles.heading}><h1>Your library.</h1><p>A place for what’s next,<br />what’s underway, and what stays with you.</p></header>
    {!checked || session.account === undefined || session.isTransitioning ? session.error ? <ErrorState title="Session unavailable." onRetry={() => void refresh()} /> : <LoadingState label="Checking your session…" /> : !session.account ? <section className={styles.empty}><p>Your library is private.</p><Link className={styles.link} href="/login?returnTo=%2Flibrary">Log in to view your library</Link></section> : <LibraryContent key={session.account.id} accountId={session.account.id} />}
  </div>;
}
function LibraryContent({ accountId }: LibraryContentProps) {
  const search = useSearchParams(); const router = useRouter(); const session = useSession();
  const filters = libraryFilters(new URLSearchParams(search.toString()));
  const library = useLibrary(accountId, filters);
  const unauthorized = library.error instanceof ApiError && library.error.status === 401;
  const { refresh } = session;
  useEffect(() => { if (unauthorized) void refresh().catch(() => {}); }, [unauthorized, refresh]);
  if (!filters) return <><div role="alert">Invalid library filters.</div><Link className={styles.link} href="/library">Reset filters</Link></>;
  if (unauthorized) return <><p role="alert">Your session expired.</p><Link className={styles.link} href="/login?returnTo=%2Flibrary">Log in to view your library</Link></>;
  const href = (page: number) => libraryHref({ ...filters, page });
  return <>
    <form key={search.toString()} className={styles.filters} aria-label="Library filters" onSubmit={event => {
      event.preventDefault(); const data = new FormData(event.currentTarget);
      router.push(libraryHref({ status: String(data.get("status")) as TrackingStatus | "", type: String(data.get("type")), page: 1, pageSize: 20 }));
    }}><div><label htmlFor="library-status">Status</label><Select id="library-status" name="status" defaultValue={filters.status}><option value="">All statuses</option>{Object.entries(trackingStatuses).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</Select></div>
      <div><label htmlFor="library-type">Media type</label><Select id="library-type" name="type" defaultValue={filters.type}><option value="">All types</option>{Object.entries(mediaTypes).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</Select></div><Button type="submit"><IconFilter size={17} aria-hidden="true" />Apply filters</Button>
    </form>
    {library.isPending ? <LoadingState label="Loading your library…" /> : library.isError ? <ErrorState title="Library unavailable." description="Please wait if requests are limited, then try again." onRetry={() => void library.refetch()} /> : !library.data.items.length ? <LibraryEmpty filters={filters} /> : <>
      <p className={styles.summary}>{library.data.totalCount} {library.data.totalCount === 1 ? "title" : "titles"} · Page {filters.page}</p>
      <ul className={styles.libraryList}>{library.data.items.map(item => <li key={item.media.id}><div className={styles.thumbnail}><MediaArtwork reference={item.media.imageReference} title={item.media.title} type={item.media.type} /></div><div className={styles.libraryInfo}><Link href={`/catalog/${item.media.id}`}>{item.media.title}</Link><p>{mediaLabels[item.media.type]} · {trackingStatuses[item.status]}</p>{item.startedAt && <p>Started <time dateTime={item.startedAt}>{item.startedAt}</time></p>}{item.completedAt && <p>Completed <time dateTime={item.completedAt}>{item.completedAt}</time></p>}<p>Open the title to edit your activity.</p></div></li>)}</ul>
      <nav className={styles.pagination} aria-label="Library pages">{filters.page > 1 ? <Link href={href(filters.page - 1)}>Previous</Link> : <span>Previous</span>}<span>Page {filters.page}</span>{filters.page < Math.ceil(library.data.totalCount / 20) && filters.page < 10000 ? <Link href={href(filters.page + 1)}>Next</Link> : <span>Next</span>}</nav>
    </>}
  </>;
}
function LibraryEmpty({ filters }: LibraryEmptyProps) {
  return <section className={styles.empty}><h2>{filters.page > 1 ? "This page has no titles." : filters.status || filters.type ? "No matching library titles." : "Your library starts here."}</h2><p>Find a title in the catalog and add it to your library.</p>{filters.page > 1 && <Link className={styles.link} href={libraryHref({ ...filters, page: 1 })}>Go to first page</Link>}{(filters.status || filters.type) && <Link className={styles.link} href="/library">Clear filters</Link>}<Link className={styles.link} href="/catalog">Browse the catalog</Link></section>;
}
