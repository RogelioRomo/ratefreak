"use client";
import Link from "next/link";
import { useEffect, useRef, type MouseEvent } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { IconFilter } from "@tabler/icons-react";
import { useSession } from "@/features/auth/hooks/use-session";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import { ApiError } from "@/shared/api/api-error";
import { useHistory } from "../hooks/use-history";
import { historyHref, monthNames, parseHistoryFilters } from "../query/history";
import type { HistoryContentProps } from "../interfaces/history";
import { HistoryTimeline } from "./history-timeline";
import personal from "./personal.module.css";
import styles from "./history.module.css";

export function HistoryContent({ accountId }: HistoryContentProps) {
  const params = useSearchParams(); const router = useRouter(); const { refresh } = useSession();
  const filters = parseHistoryFilters(new URLSearchParams(params.toString()));
  const history = useHistory(accountId, filters);
  const results = useRef<HTMLParagraphElement>(null);
  const focusAfterNavigation = useRef(false);
  const errorTarget = useRef<HTMLDivElement>(null);
  const unauthorized = history.error instanceof ApiError && history.error.status === 401;
  useEffect(() => { if (unauthorized) void refresh().catch(() => {}); }, [unauthorized, refresh]);
  const data = history.data;
  const page = filters?.page ?? 1;
  const lastPage = data ? Math.max(1, Math.ceil(data.totalCount / 20)) : 1;
  const repairedHref = data && filters ? historyHref({ ...filters, year: data.year, page: lastPage }) : null;
  useEffect(() => {
    if (history.isFetching) return;
    if (history.isError) {
      if (focusAfterNavigation.current) { errorTarget.current?.focus(); focusAfterNavigation.current = false; }
      return;
    }
    if (!data) return;
    if (page > lastPage && repairedHref) {
      focusAfterNavigation.current = true;
      router.replace(repairedHref, { scroll: false });
      return;
    }
    if (focusAfterNavigation.current) { results.current?.focus(); focusAfterNavigation.current = false; }
  }, [data, history.isFetching, history.isError, page, lastPage, repairedHref, router]);
  const currentHref = filters ? historyHref(filters) : null;
  function prepareNavigation(event: MouseEvent<HTMLAnchorElement>) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    if (event.currentTarget.getAttribute("href") === currentHref) results.current?.focus();
    else focusAfterNavigation.current = true;
  }
  if (!filters) return <><p role="alert">Invalid history filters.</p><Link className={personal.link} href="/library/history">Reset history filters</Link></>;
  if (unauthorized) return <div ref={errorTarget} tabIndex={-1} aria-label="History error"><p role="alert">Your session expired.</p><Link className={personal.link} href={`/login?returnTo=${encodeURIComponent(historyHref(filters))}`}>Log in to view history</Link></div>;
  if (history.isPending) return <LoadingState label="Loading your history…" />;
  if (history.isError) return <div ref={errorTarget} tabIndex={-1} aria-label="History error"><ErrorState title="History unavailable." description="We couldn’t refresh your completion history. Try again." onRetry={() => void history.refetch()} /></div>;
  if (!data) return null;
  if (page > lastPage) return <LoadingState label="Loading the last available page…" />;
  const selectedYear = filters.year ?? data.year;
  const years = [...new Set([...data.availableYears, selectedYear])].sort((a, b) => b - a);
  const period = data.month ? `${monthNames[data.month - 1]} ${data.year}` : `All months, ${data.year}`;
  const href = (nextPage: number) => historyHref({ ...filters, year: data.year, page: nextPage });
  return <div aria-busy={history.isFetching}>
    <p className={styles.explanation}>Your currently completed titles, grouped by their completion dates. Editing dates, changing status or removing a title updates this recap.</p>
    <form key={`${params.toString()}:${data.year}`} className={`${personal.filters} ${styles.filters}`} aria-label="History filters" onSubmit={event => {
      event.preventDefault(); const values = new FormData(event.currentTarget);
      const target = historyHref({ year: Number(values.get("year")), ...(values.get("month") ? { month: Number(values.get("month")) } : {}), page: 1, pageSize: 20 });
      if (target === currentHref) results.current?.focus();
      else { focusAfterNavigation.current = true; router.push(target); }
    }}>
      <div><label htmlFor="history-year">Year</label><Select id="history-year" name="year" defaultValue={String(selectedYear)}>{years.map(year => <option key={year} value={String(year)}>{year}</option>)}</Select></div>
      <div><label htmlFor="history-month">Month</label><Select id="history-month" name="month" defaultValue={filters.month === undefined ? "" : String(filters.month)}><option value="">All months</option>{monthNames.map((name, index) => <option key={name} value={String(index + 1)}>{name}</option>)}</Select></div>
      <Button type="submit"><IconFilter size={17} aria-hidden="true" />Apply filters</Button>
    </form>
    <section className={styles.annual} aria-label="Annual summary">
      <div className={styles.annualHeading}><h2>{data.year} by month.</h2><p>{data.annualTotal} {data.annualTotal === 1 ? "title" : "titles"} completed in {data.year}</p></div>
      <nav aria-label={`Months in ${data.year}`}><ol className={styles.months}>{data.months.map(month => <li key={month.month}><Link onClick={prepareNavigation} href={historyHref({ year: data.year, month: month.month, page: 1, pageSize: 20 })} aria-label={`${monthNames[month.month - 1]}: ${month.count} completed`} aria-current={data.month === month.month ? "date" : undefined}><span>{monthNames[month.month - 1]}</span><strong>{month.count}</strong></Link></li>)}</ol></nav>
      {data.month !== null && <Link className={personal.link} onClick={prepareNavigation} href={historyHref({ year: data.year, page: 1, pageSize: 20 })}>Show all months in {data.year}</Link>}
    </section>
    {data.missingDateCount > 0 && <p className={styles.missing}>{data.missingDateCount} completed {data.missingDateCount === 1 ? "title has" : "titles have"} no completion date across your library. They aren’t included in dated totals. <Link href="/library?status=completed">Review completed titles</Link></p>}
    <p ref={results} tabIndex={-1} aria-label="History results" className={personal.summary}>{data.totalCount} {data.totalCount === 1 ? "title" : "titles"} · {period} · Page {page}</p>
    {data.items.length ? <HistoryTimeline items={data.items} /> : <section className={personal.empty}><h2>No dated completions in this period.</h2><p>Choose another period, or add a completion date to a completed title.</p><Link className={personal.link} href="/library">Open your library</Link></section>}
    {lastPage > 1 && <nav className={personal.pagination} aria-label="History pages">{page > 1 ? <Link onClick={prepareNavigation} href={href(page - 1)}>Previous</Link> : <span>Previous</span>}<span>Page {page} of {lastPage}</span>{page < lastPage && page < 10000 ? <Link onClick={prepareNavigation} href={href(page + 1)}>Next</Link> : <span>Next</span>}</nav>}
  </div>;
}
