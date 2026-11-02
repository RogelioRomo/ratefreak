"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useSession } from "@/features/auth/hooks/use-session";
import { LoadingState } from "@/components/ui/loading-state";
import { Button } from "@/components/ui/button";
import type { CollectionSessionProps } from "../interfaces/component-props";
export function CollectionSession({children, requireAccount = false, returnTo}: CollectionSessionProps) {
  const session = useSession();
  const {refresh} = session;
  const [checked, setChecked] = useState(false);
  const verified = session.account === undefined ? undefined : session.account?.id ?? "anonymous";
  const [remembered, setRemembered] = useState<string | undefined>(verified);
  const viewer = verified ?? remembered;
  if (verified !== undefined && remembered !== verified) setRemembered(verified);
  useEffect(() => { let live = true; void refresh().catch(() => {}).finally(() => { if (live) setChecked(true); }); return () => {live = false;}; }, [refresh]);
  const active = checked && verified !== undefined && !session.error && !session.isTransitioning && !session.isLoading;
  const permitted = !!viewer && (!requireAccount || viewer !== "anonymous");
  return <>
    {session.error ? <section><p role="alert">Your session could not be checked.</p><Button variant="secondary" onClick={() => void refresh()}>Retry session</Button></section>
      : !active ? <LoadingState label="Checking your session…" />
      : requireAccount && viewer === "anonymous" ? <section><p>Sign in to manage your collections.</p><Link href={`/login?${new URLSearchParams({returnTo})}`}>Log in to continue</Link></section> : null}
    {permitted && <div key={viewer} hidden={!active} inert={!active}><fieldset disabled={!active} style={{border:0, padding:0, margin:0, minWidth:0}}>{children({viewer: viewer!, accountId: viewer === "anonymous" ? null : viewer!, active})}</fieldset></div>}
  </>;
}
