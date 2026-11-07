"use client";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { getLinkedProviders } from "../api/external";
import { useSession } from "../hooks/use-session";
import { ProviderOptions } from "./provider-options";
import styles from "./external.module.css";
export function LinkedProviderSettings() {
  const session = useSession();
  const query = useQuery({ queryKey: ["external-logins", session.account?.id], queryFn: ({ signal }) => getLinkedProviders(signal), enabled: !!session.account, retry: false });
  if (!session.account) return null;
  return <section className={styles.settings} aria-labelledby="linked-sign-ins"><h2 id="linked-sign-ins">Sign-in options</h2>
    <p>Link another way to sign in to this account.</p>
    {query.isPending ? <p role="status">Loading linked accounts…</p> : query.error ? <div><p>We couldn’t load your linked accounts.</p><Button variant="secondary" onClick={() => { void query.refetch(); }}>Retry linked accounts</Button></div> : <ProviderOptions linked={query.data.providers} />}
  </section>;
}
