"use client";

import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import { useSession } from "../hooks/use-session";
import styles from "./auth.module.css";
import Link from "next/link";


export function AccountDetails() {
  const session = useSession();
  if (session.error) return <ErrorState title="We couldn’t load your account" description="Please try again." onRetry={() => { void session.refresh(); }} />;
  if (!session.account) return <LoadingState label="Checking your session…" />;
  const account = session.account;
  return <div className={styles.details}>
    <dl>
      <div><dt>Display name</dt><dd>{account.displayName}</dd></div>
      <div><dt>Handle</dt><dd>@{account.handle}</dd></div>
      <div><dt>Email</dt><dd>{account.email}</dd></div>
    </dl>
    <p>Your email is private. Your display name and bio are public. Logging out signs you out on all devices.</p>
    <Link href={`/users/${account.handle}`}>View public profile</Link>
  </div>;
}
