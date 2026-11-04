"use client";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { useSession } from "@/features/auth/hooks/use-session";
import { sessionQueryKey } from "@/features/auth/session/query-keys";
import type { AccountProfile } from "@/features/auth/interfaces/account-profile";
import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import { useConnections } from "../hooks/use-connections";
import { connectionFilters, connectionHref } from "../query/filters";
import { FollowActions } from "./follow-actions";
import type { ConnectionsProps } from "../interfaces/follows";
import styles from "./follows.module.css";
export function Connections({ userId, handle }: ConnectionsProps) {
 const search = useSearchParams(), session = useSession(), client = useQueryClient();
 const filters = connectionFilters(new URLSearchParams(search.toString())); const list = useConnections(userId, filters);
 const cached = client.getQueryData<AccountProfile | null>(sessionQueryKey);
 const active = !!session.account && !session.error && !session.isTransitioning;
 return <div className={styles.connections}>
  {cached && cached.id.toLowerCase() !== userId.toLowerCase() && <FollowActions key={`${cached.id}:${userId}`} userId={userId} handle={handle} available={active} />}
  {session.account === null && <p><Link href={`/login?returnTo=${encodeURIComponent(`/users/${handle}`)}`}>Log in to follow @{handle}</Link></p>}
  <nav className={styles.tabs} aria-label="Profile connections">{(["followers", "following"] as const).map(direction => <Link key={direction} href={connectionHref(handle, { direction, page: 1 })} aria-current={filters?.direction === direction ? "page" : undefined}>{direction === "followers" ? "Followers" : "Following"}</Link>)}</nav>
  {!filters ? <><p role="alert">Invalid connection filters.</p><Link href={`/users/${handle}`}>Reset connections</Link></> : list.isPending ? <LoadingState label="Loading connections…" /> : list.isError ? <ErrorState title="Connections unavailable" description="The account may be missing, or requests may be limited. Wait and try again." onRetry={() => void list.refetch()} /> : <>
   <p className={styles.summary}>{filters.direction === "followers" ? "Followers" : "Following"}: {list.data.totalCount}</p>
   {list.data.items.length ? <ul className={styles.list}>{list.data.items.map(user => <li key={user.id}><Link href={`/users/${user.handle}`}>{user.displayName}</Link><p>@{user.handle}</p></li>)}</ul> : <p>{filters.page === 1 ? "No connections to show yet." : "No connections on this page."}</p>}
   <nav className={styles.paging} aria-label="Connection pages">{filters.page > 1 && <Link href={connectionHref(handle, { ...filters, page: filters.page - 1 })}>Previous</Link>}{filters.page < 10000 && filters.page * 20 < list.data.totalCount && <Link href={connectionHref(handle, { ...filters, page: filters.page + 1 })}>Next</Link>}{filters.page > 1 && !list.data.items.length && <Link href={connectionHref(handle, { ...filters, page: 1 })}>First page</Link>}</nav>
  </>}
 </div>;
}
