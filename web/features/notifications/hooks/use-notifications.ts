"use client";
import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSession } from "@/features/auth/hooks/use-session";
import { ApiError } from "@/shared/api/api-error";
import { getNotifications, getUnreadCount } from "../api/notifications";
export const notificationKeys = {
  account: (id: string) => ["notifications", id] as const,
  count: (id: string) => ["notifications", id, "count"] as const,
  page: (id: string, page: number) => ["notifications", id, "inbox", page] as const,
};
export function useNotificationCount(poll = false) {
  const session = useSession();
  const id = session.account?.id ?? "";
  const query = useQuery({ queryKey: notificationKeys.count(id), queryFn: ({ signal }) => getUnreadCount(signal),
    enabled: !!id && !session.isTransitioning && !session.error, staleTime: 60_000, retry: false,
    refetchOnWindowFocus: poll ? "always" : false, refetchInterval: poll ? 60_000 : false, refetchIntervalInBackground: false });
  const { refresh } = session;
  const unauthorized = query.error instanceof ApiError && query.error.status === 401;
  useEffect(() => { if (unauthorized) void refresh().catch(() => {}); }, [unauthorized, refresh]);
  return query;
}
// Mounted once under SessionProvider; desktop and mobile links only observe this shared query.
export function NotificationSync() { useNotificationCount(true); return null; }
export function useNotifications(id: string, page: number) {
  return useQuery({ queryKey: notificationKeys.page(id, page), queryFn: ({ signal }) => getNotifications(page, signal),
    enabled: !!id, staleTime: 60_000, retry: false, refetchOnWindowFocus: "always", refetchInterval: 60_000, refetchIntervalInBackground: false });
}
