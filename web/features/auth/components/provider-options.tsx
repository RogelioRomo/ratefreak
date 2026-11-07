"use client";

import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/shared/api/api-error";
import { navigateToProvider } from "../api/external-navigation";
import { getProviders, startExternal } from "../api/external";
import { useSession } from "../hooks/use-session";
import type { ExternalProvider } from "../interfaces/external-provider";
import type { ProviderOptionsProps } from "../interfaces/provider-options-props";
import styles from "./external.module.css";
const label = { google: "Google", github: "GitHub" };
export function ProviderOptions({ linked }: ProviderOptionsProps) {
  const session = useSession();
  const query = useQuery({ queryKey: ["external", "providers"], queryFn: ({ signal }) => getProviders(signal), staleTime: 60_000, retry: false });
  const busy = useRef(false);
  const [starting, setStarting] = useState<ExternalProvider | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    const restored = (event: PageTransitionEvent) => {
      if (!event.persisted) return;
      busy.current = false; setStarting(null); setError(null);
    };
    window.addEventListener("pageshow", restored);
    return () => window.removeEventListener("pageshow", restored);
  }, []);
  async function start(provider: ExternalProvider) {
    if (busy.current || session.isTransitioning) return;
    busy.current = true; setStarting(provider); setError(null);
    try {
      const navigate = async () => {
        const url = await startExternal(provider, linked !== undefined);
        navigateToProvider(url);
      };
      if (linked !== undefined) await navigate();
      else await session.runExclusive(navigate);
    } catch (failure) {
      setError(failure instanceof ApiError && failure.status === 409
        ? "Sign out before starting another sign-in, or link this provider from your existing account."
        : failure instanceof ApiError && failure.status === 401 && linked !== undefined
          ? "Your session has expired. Log in again before linking a provider."
          : failure instanceof ApiError && failure.status === 429
            ? "Too many attempts. Please wait before trying again."
            : "We couldn’t start that sign-in. Please try again.");
      busy.current = false; setStarting(null);
    }
  }
  const available = query.data?.filter(item => item.enabled && !linked?.includes(item.name)) ?? [];
  if (!query.isPending && !query.error && !available.length && !linked?.length) return null;
  return <section className={styles.options} aria-label={linked ? "Linked sign-in options" : "Other sign-in options"}>
    {linked?.map(provider => <p key={provider}>{label[provider]} is linked</p>)}
    {query.isPending && <p role="status">Loading sign-in options…</p>}
    {query.error && <div className={styles.notice}><p>Other sign-in options are unavailable.</p><Button variant="secondary" onClick={() => { void query.refetch(); }}>Retry sign-in options</Button></div>}
    <div className={styles.buttons}>{available.map(item => <Button key={item.name} variant="secondary" disabled={starting !== null || session.isTransitioning} onClick={() => { void start(item.name); }}>
      {starting === item.name ? `Connecting to ${label[item.name]}…` : `${linked ? "Link" : "Continue with"} ${label[item.name]}`}
    </Button>)}</div>
    {starting && <p role="status">Opening {label[starting]}…</p>}
    {error && <p role="alert" className={styles.error}>{error}</p>}
  </section>;
}
