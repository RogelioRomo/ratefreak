"use client";
import Link from "next/link";
import { useEffect, useLayoutEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useSession } from "@/features/auth/hooks/use-session";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { historyHref, parseHistoryFilters } from "../query/history";
import { LibraryNavigation } from "./library-navigation";
import { HistoryContent } from "./history-content";
import styles from "./personal.module.css";

export function HistoryScreen() {
  const session = useSession(); const { refresh } = session;
  const params = useSearchParams();
  const filters = parseHistoryFilters(new URLSearchParams(params.toString()));
  const returnTo = historyHref(filters ?? { page: 1, pageSize: 20 });
  const [checked, setChecked] = useState(false);
  // Next's Activity preserves state while hiding routes. Require a fresh entry
  // check before revealing a preserved private page again.
  useLayoutEffect(() => () => { setChecked(false); }, []);
  useEffect(() => { let active = true; void refresh().catch(() => {}).finally(() => { if (active) setChecked(true); }); return () => { active = false; }; }, [refresh]);
  return <div className={styles.screen}>
    <header className={styles.heading}><h1>Your history.</h1><p>Look back at what you’ve finished,<br />one month at a time.</p></header>
    <LibraryNavigation current="history" />
    {session.error ? <ErrorState title="Session unavailable." onRetry={() => void refresh()} /> : !checked || session.isLoading || session.account === undefined || session.isTransitioning ? <LoadingState label="Checking your session…" /> : !session.account ? <section className={styles.empty}><p>Your completion history is private.</p><Link className={styles.link} href={`/login?returnTo=${encodeURIComponent(returnTo)}`}>Log in to view history</Link></section> : <HistoryContent key={session.account.id} accountId={session.account.id} />}
  </div>;
}
