"use client";
import { Select } from "@/components/ui/select";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { IconFilter } from "@tabler/icons-react";
import { useSession } from "@/features/auth/hooks/use-session";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { Button } from "@/components/ui/button";
import { mediaTypes } from "@/features/catalog/query/filters";
import { ApiError } from "@/shared/api/api-error";
import { personalKeys, useLibrary } from "../hooks/use-personal";
import { libraryFilters, libraryHref, librarySorts, trackingStatuses } from "../query/filters";
import type { LibraryItem, LibrarySort, TrackingStatus } from "../interfaces/personal";
import type { LibraryContentProps, LibraryEmptyProps } from "../interfaces/component-props";
import { LibraryRow } from "./library-row";
import styles from "./personal.module.css";

export function LibraryScreen() {
  const session = useSession(); const { refresh } = session;
  const [checked, setChecked] = useState(false);
  useEffect(() => { let active = true; void refresh().catch(() => {}).finally(() => { if (active) setChecked(true); }); return () => { active = false; }; }, [refresh]);
  return <div className={styles.screen}>
    <header className={styles.heading}><h1>Your library.</h1><p>A place for what’s next,<br />what’s underway, and what stays with you.</p></header>
    {session.error ? <ErrorState title="Session unavailable." onRetry={() => void refresh()} /> : !checked || session.account === undefined || session.isTransitioning ? <LoadingState label="Checking your session…" /> : !session.account ? <section className={styles.empty}><p>Your library is private.</p><Link className={styles.link} href="/login?returnTo=%2Flibrary">Log in to view your library</Link></section> : <LibraryContent key={session.account.id} accountId={session.account.id} />}
  </div>;
}

function LibraryContent({ accountId }: LibraryContentProps) {
  const search = useSearchParams(); const router = useRouter(); const session = useSession();
  const filters = libraryFilters(new URLSearchParams(search.toString()));
  const library = useLibrary(accountId, filters);
  const client = useQueryClient();
  const results = useRef<HTMLParagraphElement>(null);
  const focusAfterSave = useRef<string | null>(null);
  const [message, setMessage] = useState("");
  const unauthorized = library.error instanceof ApiError && library.error.status === 401;
  const { refresh } = session;
  useEffect(() => { if (unauthorized) void refresh().catch(() => {}); }, [unauthorized, refresh]);
  const lastPage = library.data ? Math.max(1, Math.ceil(library.data.totalCount / 20)) : null;
  const currentPage = filters?.page;
  const currentHref = filters && lastPage !== null ? libraryHref({ ...filters, page: lastPage }) : null;
  useEffect(() => {
    if (library.isFetching || library.isError || !library.data) return;
    if (currentPage && lastPage !== null && currentPage > lastPage && currentHref) {
      router.replace(currentHref, { scroll: false });
      return;
    }
    if (focusAfterSave.current) {
      if (!library.data.items.some(item => item.media.id === focusAfterSave.current)) results.current?.focus();
      else document.getElementById(`status-${focusAfterSave.current}`)?.focus();
      focusAfterSave.current = null;
    }
  }, [library.data, library.isFetching, library.isError, currentPage, lastPage, currentHref, router]);
  function saved(item: LibraryItem, restoreFocus: boolean) {
    setMessage(`Status saved for ${item.media.title}. ${trackingStatuses[item.status]}.`);
    focusAfterSave.current = restoreFocus ? item.media.id : null;
    void Promise.all([
      client.invalidateQueries({ queryKey: personalKeys.tracking(accountId, item.media.id) }),
      client.invalidateQueries({ queryKey: personalKeys.library(accountId) }),
    ]);
  }
  if (!filters) return <><div role="alert">Invalid library filters.</div><Link className={styles.link} href="/library">Reset filters</Link></>;
  if (unauthorized) return <><p role="alert">Your session expired.</p><Link className={styles.link} href="/login?returnTo=%2Flibrary">Log in to view your library</Link></>;
  const href = (page: number) => libraryHref({ ...filters, page });
  const narrowed = !!(filters.status || filters.type || filters.q);
  return <>
    <form key={search.toString()} className={`${styles.filters} ${styles.libraryFilters}`} aria-label="Library filters" onSubmit={event => {
      event.preventDefault(); const data = new FormData(event.currentTarget);
      router.push(libraryHref({ status: String(data.get("status")) as TrackingStatus | "", type: String(data.get("type")), q: String(data.get("q")).trim(), sort: String(data.get("sort")) as LibrarySort, page: 1, pageSize: 20 }));
    }}>
      <div className={styles.librarySearch}><label htmlFor="library-search">Search your library</label><input id="library-search" name="q" type="search" maxLength={200} defaultValue={filters.q ?? ""} placeholder="Find a saved title" /></div>
      <div><label htmlFor="library-status">Status</label><Select id="library-status" name="status" defaultValue={filters.status}><option value="">All statuses</option>{Object.entries(trackingStatuses).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</Select></div>
      <div><label htmlFor="library-type">Media type</label><Select id="library-type" name="type" defaultValue={filters.type}><option value="">All types</option>{Object.entries(mediaTypes).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</Select></div>
      <div><label htmlFor="library-sort">Sort by</label><Select id="library-sort" name="sort" defaultValue={filters.sort ?? "recently_added"}>{Object.entries(librarySorts).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</Select></div>
      <Button type="submit"><IconFilter size={17} aria-hidden="true" />Apply filters</Button>
    </form>
    <p role="status" aria-live="polite" className={styles.libraryMessage}>{message}</p>
    <p ref={results} tabIndex={-1} aria-label="Library results" className={styles.summary}>{!library.isError && library.data ? `${library.data.totalCount} ${library.data.totalCount === 1 ? "title" : "titles"} · Page ${filters.page}` : "Your saved titles"}{narrowed && <Link className={styles.link} href={libraryHref({ status: "", type: "", sort: filters.sort, page: 1, pageSize: 20 })}>Clear filters</Link>}</p>
    {library.isPending ? <LoadingState label="Loading your library…" /> : library.isError ? <ErrorState title="Library unavailable." description="Please wait if requests are limited, then try again." onRetry={() => void library.refetch()} /> : !library.data.items.length ? <LibraryEmpty filters={filters} /> : <>
      <ul className={styles.libraryList}>{library.data.items.map(item => <LibraryRow key={item.media.id} item={item} accountId={accountId} onSaved={saved} onSaving={() => setMessage("")} />)}</ul>
      <nav className={styles.pagination} aria-label="Library pages">{filters.page > 1 ? <Link href={href(filters.page - 1)}>Previous</Link> : <span>Previous</span>}<span>Page {filters.page}</span>{filters.page < Math.ceil(library.data.totalCount / 20) && filters.page < 10000 ? <Link href={href(filters.page + 1)}>Next</Link> : <span>Next</span>}</nav>
    </>}
  </>;
}

function LibraryEmpty({ filters }: LibraryEmptyProps) {
  return <section className={styles.empty}>
    <h2>{filters.page > 1 ? "This page has no titles." : filters.status || filters.type || filters.q ? "No matching library titles." : "Your library starts here."}</h2>
    <p>{filters.status || filters.type || filters.q ? "Try another search or adjust your filters." : "Find a title in the catalog and add it to your library."}</p>
    {filters.page > 1 && <Link className={styles.link} href={libraryHref({ ...filters, page: 1 })}>Go to first page</Link>}
    <Link className={styles.link} href="/catalog">Browse the catalog</Link>
  </section>;
}
