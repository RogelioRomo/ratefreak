"use client";
import { Avatar } from "@/features/profiles/components/avatar";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useSession } from "@/features/auth/hooks/use-session";
import { usePersonalWrite } from "@/features/personal/hooks/use-personal-write";
import { Button } from "@/components/ui/button";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { ApiError } from "@/shared/api/api-error";
import { clearNotifications, deleteNotification, readAllNotifications, setNotificationRead } from "../api/notifications";
import { notificationKeys, useNotifications } from "../hooks/use-notifications";
import { useOpenNotification } from "../hooks/use-open-notification";
import { ClearInboxDialog } from "./clear-inbox-dialog";
import styles from "./notifications.module.css";
export function NotificationsScreen() {
  const session = useSession();
  return <div className={styles.screen}><header className={styles.heading}><h1>Notifications.</h1><p>New connections and conversations,<br />all in one place.</p></header>
    {session.account === undefined || session.isTransitioning ? session.error ? <ErrorState title="Session unavailable." onRetry={() => void session.refresh()} /> : <LoadingState label="Checking your session…" />
      : session.account === null ? <section className={styles.empty}><p>Your notifications are private.</p><Link href="/login?returnTo=%2Fnotifications">Log in to view notifications</Link></section>
      : <Inbox key={session.account.id} accountId={session.account.id} />}
  </div>;
}
function Inbox({ accountId }: { accountId: string }) {
  const [page, setPage] = useState(1); const [confirm, setConfirm] = useState(false); const [message, setMessage] = useState("");
  const openNotification = useOpenNotification();
  const query = useNotifications(accountId, page); const write = usePersonalWrite(); const { refresh } = useSession();
  const unauthorized = query.error instanceof ApiError && query.error.status === 401;
  useEffect(() => { if (unauthorized) void refresh().catch(() => {}); }, [unauthorized, refresh]);
  const validPage = Math.max(1, Math.ceil((query.data?.totalCount ?? 0) / 20));
  if (query.data && !query.isFetching && page > validPage) setPage(validPage);
  async function perform(operation: (signal: AbortSignal) => Promise<void>, success: string) {
    setMessage("");
    const result = await write.run(operation, [notificationKeys.account(accountId)]);
    if (result) { setMessage(success); setConfirm(false); }
    else setConfirm(false);
  }
  if (unauthorized) return <p role="alert">Your session expired. <Link href="/login?returnTo=%2Fnotifications">Log in to view notifications</Link></p>;
  return <>
    <div className={styles.toolbar}><p>{query.data ? `${query.data.unreadCount} unread` : "Your inbox"}</p><div className={styles.actions}>
      <Button variant="secondary" disabled={write.pending || !query.data?.unreadCount || query.isError} onClick={() => void perform(readAllNotifications, "All available notifications marked as read.")}>Mark all as read</Button>
      <Button variant="secondary" disabled={write.pending} onClick={() => { write.clearError(); setConfirm(true); }}>Clear inbox</Button>
    </div></div>
    <p role="status" className={styles.feedback}>{message}</p>
    {write.error && <p role="alert" className={styles.error}>We couldn’t confirm that change. Refresh your inbox before trying again. <Button variant="secondary" onClick={() => { void query.refetch(); void write.client.invalidateQueries({queryKey: notificationKeys.count(accountId)}); }}>Refresh inbox</Button></p>}
    {query.isError && <ErrorState title="Notifications unavailable." description="Please wait if requests are limited, then try again." onRetry={() => void query.refetch()} />}
    {query.isPending ? <LoadingState label="Loading notifications…" /> : query.data && <>
      {!query.data.items.length ? <section className={styles.empty}><h2>You’re all caught up.</h2><p>New followers and comments on your reviews will appear here.</p></section> : <ul className={styles.list}>{query.data.items.map(item => <li key={item.id} className={item.readAt ? undefined : styles.unread}>
        <div className={styles.event}><Avatar reference={item.actor.avatarUrl} /><span className={styles.state}>{item.readAt ? "Read" : "Unread"}</span>
          <Link href={item.kind === "follow" ? `/users/${encodeURIComponent(item.actor.handle)}` : `/reviews/${item.reviewId}`}
            onClick={() => { if (!item.readAt) void openNotification(item.id).then(saved => { if (!saved) setMessage("We couldn’t mark that notification as read. Try the read control in your inbox."); }); }}>
            {item.actor.displayName} {item.kind === "follow" ? "started following you." : "commented on your review."}</Link>
          <time dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString("en-US", { dateStyle: "medium", timeStyle: "short" })}</time>
        </div><div className={styles.actions}>
          <Button variant="secondary" disabled={write.pending} onClick={() => void perform(signal => setNotificationRead(item.id, !item.readAt, signal), item.readAt ? "Notification marked as unread." : "Notification marked as read.")}>{item.readAt ? "Mark as unread" : "Mark as read"}</Button>
          <Button variant="secondary" disabled={write.pending} onClick={() => void perform(signal => deleteNotification(item.id, signal), "Notification deleted.")}>Delete notification</Button>
        </div>
      </li>)}</ul>}
      {query.data.totalCount > 20 && <nav className={styles.pagination} aria-label="Notification pages"><Button variant="secondary" disabled={page === 1 || write.pending} onClick={() => setPage(page - 1)}>Previous</Button><span>Page {page} of {validPage}</span><Button variant="secondary" disabled={page >= validPage || page >= 10000 || write.pending} onClick={() => setPage(page + 1)}>Next</Button></nav>}
    </>}
    {confirm && <ClearInboxDialog pending={write.pending} onCancel={() => setConfirm(false)} onConfirm={() => void perform(clearNotifications, "Inbox cleared.")} />}
  </>;
}
