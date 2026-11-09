"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useSession } from "../hooks/use-session";
import { useQueryClient } from "@tanstack/react-query";
import type { AccountProfile } from "../interfaces/account-profile";
import { sessionQueryKey } from "../session/query-keys";
import { AccountSections } from "./account-sections";
import styles from "./account-settings.module.css";
import { AccountDetails } from "./account-details";
import { LoadingState } from "@/components/ui/loading-state";


export function AccountScreen() {
  const session = useSession();
  const router = useRouter();
  const client = useQueryClient();
  const cached = client.getQueryData<AccountProfile | null>(sessionQueryKey);
  const [entryChecked, setEntryChecked] = useState(false);
  // This screen outlives its private panels when the identity cache is cleared.
  // Hold its ordinary login redirect until an explicit deletion has either
  // failed or navigated to the public confirmation page.
  const deletionNavigation = useRef(false);
  const onDeletionPendingChange = useCallback((pending: boolean) => {
    deletionNavigation.current = pending;
  }, []);
  const { refresh } = session;
  useEffect(() => {
    let active = true;
    void refresh().finally(() => { if (active) setEntryChecked(true); });
    return () => { active = false; };
  }, [refresh]);
  useEffect(() => {
    if (!deletionNavigation.current && entryChecked && session.account === null && !session.isLoading && !session.isTransitioning && !session.error) router.replace("/login");
  }, [entryChecked, session.account, session.isLoading, session.isTransitioning, session.error, router]);
  return <section className={styles.account}>
    <header className={styles.heading}><h1>Your account.</h1><p>A space that’s yours.</p></header>
    {entryChecked ? cached ? <AccountSections key={cached.id} onDeletionPendingChange={onDeletionPendingChange} /> : <AccountDetails /> : <LoadingState label="Checking your session…" />}
  </section>;
}
