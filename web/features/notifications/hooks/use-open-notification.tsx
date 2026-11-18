"use client";
import { createContext, useContext, useLayoutEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useSession } from "@/features/auth/hooks/use-session";
import { ApiError } from "@/shared/api/api-error";
import type { ChildrenProps } from "@/shared/interfaces/children-props";
import { setNotificationRead } from "../api/notifications";
import { notificationKeys } from "./use-notifications";
const OpenNotificationContext = createContext<((id: string) => Promise<boolean>) | null>(null);
// Lives with the session so navigating to the destination cannot cancel marking an entry read.
export function NotificationReadProvider({ children }: ChildrenProps) {
  const session = useSession(); const client = useQueryClient();
  const accountId = session.account?.id;
  const identity = useRef(accountId);
  const requests = useRef(new Set<AbortController>());
  useLayoutEffect(() => { identity.current = accountId; const active = requests.current; return () => { for (const controller of active) controller.abort(); active.clear(); }; }, [accountId]);
  async function open(id: string): Promise<boolean> {
    const actor = identity.current; if (!actor) return false;
    const controller = new AbortController(); requests.current.add(controller);
    try {
      await setNotificationRead(id, true, controller.signal); controller.signal.throwIfAborted();
      if (identity.current !== actor) return false;
      await client.invalidateQueries({ queryKey: notificationKeys.account(actor) });
      return true;
    } catch (error) {
      if (!controller.signal.aborted && error instanceof ApiError && error.status === 401) void session.refresh().catch(() => {});
      return false;
    } finally { requests.current.delete(controller); }
  }
  return <OpenNotificationContext.Provider value={open}>{children}</OpenNotificationContext.Provider>;
}
export function useOpenNotification() {
  const open = useContext(OpenNotificationContext);
  if (!open) throw new Error("useOpenNotification requires NotificationReadProvider.");
  return open;
}
