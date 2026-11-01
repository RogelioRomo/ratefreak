"use client";

import Link from "next/link";
import type { AuthEntryProps } from "../interfaces/login-continuation";
import { safePreviewReturnTo } from "../query/return-to";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { useSession } from "../hooks/use-session";

export function AuthEntry({ children, returnTo }: AuthEntryProps) {
  const session = useSession();
  if (session.isLoading) return <LoadingState label="Checking your session…" />;
  if (session.error) return <ErrorState title="Account service unavailable" description="Please try again." onRetry={() => { void session.refresh(); }} />;
  const continuation = safePreviewReturnTo(returnTo ?? null);
  if (session.account && continuation) return <p>You’re already logged in. <Link href={continuation}>{continuation.startsWith("/catalog/discover/") ? "Continue to preview" : continuation === "/library" ? "Continue to your library" : "Continue to title"}</Link>.</p>;
  if (session.account) return <p>You’re already logged in. <Link href="/account">Go to your account</Link>.</p>;
  return children;
}
