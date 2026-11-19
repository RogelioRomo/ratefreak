"use client";
import Link from "next/link";
import { IconBell } from "@tabler/icons-react";
import { usePathname } from "next/navigation";
import { useSession } from "@/features/auth/hooks/use-session";
import { useNotificationCount } from "../hooks/use-notifications";
import styles from "./notifications.module.css";
export function NotificationLink() {
  const session = useSession(); const count = useNotificationCount(); const pathname = usePathname();
  if (!session.account || session.isTransitioning || session.error) return null;
  const unread = count.isError ? undefined : count.data?.unreadCount;
  return <Link className={styles.notificationLink} title="Notifications" href="/notifications" aria-current={pathname === "/notifications" ? "page" : undefined}
    aria-label={unread === undefined ? "Notifications, unread count unavailable" : `Notifications, ${unread} unread`}>
    <span className={styles.bell}><IconBell size={24} stroke={1.75} aria-hidden="true"/>{unread !== undefined && unread > 0 && <span className={styles.badge} aria-hidden="true">{unread > 99 ? "99+" : unread}</span>}</span>
  </Link>;
}
