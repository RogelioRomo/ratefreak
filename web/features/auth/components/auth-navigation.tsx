"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useSession } from "../hooks/use-session";
import { LogoutControl } from "./logout-control";
import styles from "./auth-navigation.module.css";

export function AuthNavigation() {
  const session = useSession();
  const pathname = usePathname();
  return <div className={styles.links}>
    <Link href="/" aria-current={pathname === "/" ? "page" : undefined}>Home</Link>
    <Link href="/catalog" aria-current={pathname === "/catalog" || pathname.startsWith("/catalog/") ? "page" : undefined}>Catalog</Link>
    {session.account ? <><Link href="/library" aria-current={pathname === "/library" ? "page" : undefined}>Library</Link><Link href="/account" aria-current={pathname === "/account" ? "page" : undefined}>Account</Link></>
      : session.account === null ? <><Link href="/login" aria-current={pathname === "/login" ? "page" : undefined}>Log in</Link><Link href="/register" className={styles.register}>Create account</Link></>
      : <span className={styles.status}>{session.error ? <Link href="/account">Account unavailable</Link> : "Checking session…"}</span>}
    <LogoutControl />
  </div>;
}
