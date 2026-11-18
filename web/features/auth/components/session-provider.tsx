"use client";

import { NotificationReadProvider } from "@/features/notifications/hooks/use-open-notification";
import { NotificationSync } from "@/features/notifications/hooks/use-notifications";
import { useCallback, useMemo, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import type { ChildrenProps } from "@/shared/interfaces/children-props";
import type { AccountProfile } from "../interfaces/account-profile";
import type { SessionContextValue } from "../interfaces/session-context-value";
import { resetCsrfState } from "@/shared/api/csrf";
import { getCurrentAccount } from "../api/accounts";
import { SessionContext } from "../session/session-context";
import { sessionQueryKey } from "../session/query-keys";
import { applyIdentity, reconcileIdentity } from "../session/transition";
import { useSessionSync } from "../hooks/use-session-sync";

export function SessionProvider({ children }: ChildrenProps) {
  const client = useQueryClient();
  const busy = useRef(false);
  const needsRefresh = useRef(false);
  const [isTransitioning, setTransitioning] = useState(false);
  const [isSynchronizing, setSynchronizing] = useState(false);
  const identityEpoch = useRef(0);
  const synchronize = useCallback((identityChanged: boolean) => {
    if (identityChanged) {
      resetCsrfState(); identityEpoch.current++; setSynchronizing(true);
      // Cancel the previous identity's in-flight reads before resolving the new cookie.
      void client.cancelQueries().then(() => {
        if (busy.current) { needsRefresh.current = true; return; }
        void client.invalidateQueries({ queryKey: sessionQueryKey });
      });
      return;
    }
    if (busy.current) { needsRefresh.current = true; return; }
    void client.invalidateQueries({ queryKey: sessionQueryKey });
  }, [client]);
  const notifyIdentityChange = useSessionSync(synchronize);
  const query = useQuery({
    queryKey: sessionQueryKey,
    queryFn: async ({ signal }) => {
      const epoch = identityEpoch.current;
      try {
        const account = await getCurrentAccount(signal);
        signal.throwIfAborted();
        await reconcileIdentity(client, account);
        signal.throwIfAborted();
        return account;
      } finally {
        if (epoch === identityEpoch.current && !signal.aborted) setSynchronizing(false);
      }
    },
    enabled: !isTransitioning,
    staleTime: 60_000,
    retry: false,
    refetchOnWindowFocus: "always",
    refetchOnMount: "always",
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
  });

  const runExclusive = useCallback(async <T,>(operation: () => Promise<T>): Promise<T> => {
    if (busy.current) throw new Error("An account action is already in progress.");
    busy.current = true;
    setTransitioning(true);
    try { return await operation(); }
    finally {
      busy.current = false;
      if (needsRefresh.current) {
        needsRefresh.current = false;
        void client.invalidateQueries({ queryKey: sessionQueryKey });
      }
      setTransitioning(false);
    }
  }, [client]);

  const changeIdentity = useCallback((operation: () => Promise<AccountProfile | null>) => runExclusive(async () => {
    resetCsrfState();
    await client.cancelQueries();
    const account = await operation();
    await applyIdentity(client, account);
    notifyIdentityChange();
  }), [client, runExclusive, notifyIdentityChange]);

  const { refetch } = query;
  const refresh = useCallback(async () => { await refetch(); }, [refetch]);
  const isLoading = query.isPending;
  // Keep the last verified identity during routine refetches so navigation and
  // page content stay mounted. Errors and explicit account changes still gate UI.
  const account = isLoading || query.isError || isTransitioning || isSynchronizing ? undefined : query.data;
  const value = useMemo<SessionContextValue>(() => ({
    account, isLoading, isTransitioning: isTransitioning || isSynchronizing, error: query.error, refresh, runExclusive, changeIdentity,
  }), [account, isLoading, isTransitioning, isSynchronizing, query.error, refresh, runExclusive, changeIdentity]);

  return <SessionContext.Provider value={value}><NotificationReadProvider><NotificationSync />{children}</NotificationReadProvider></SessionContext.Provider>;
}
