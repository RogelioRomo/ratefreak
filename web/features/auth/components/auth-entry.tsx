"use client";

import Link from "next/link";
import type { ChildrenProps } from "@/shared/interfaces/children-props";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { useSession } from "../hooks/use-session";

export function AuthEntry({ children }: ChildrenProps) {
  const session = useSession();
  if (session.isLoading) return <LoadingState label="Checking your session…" />;
  if (session.error) return <ErrorState title="Account service unavailable" description="Please try again." onRetry={() => { void session.refresh(); }} />;
  if (session.account) return <p>You’re already logged in. <Link href="/account">Go to your account</Link>.</p>;
  return children;
}
