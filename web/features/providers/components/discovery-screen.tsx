"use client";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { useProviders, useExternalSearch } from "../hooks/use-providers";
import { parseDiscovery, discoveryHref } from "../query/filters";
import { IconArrowLeft } from "@tabler/icons-react";
import { DiscoveryResults } from "./discovery-results";
import { DiscoveryForm } from "./discovery-form";
import { ProviderAttribution } from "./provider-attribution";
import { ProviderError } from "./provider-error";
import styles from "./providers.module.css";

export function DiscoveryScreen() {
  const search = useSearchParams();
  const capabilities = useProviders();
  const parsed = capabilities.data ? parseDiscovery(new URLSearchParams(search.toString()), capabilities.data) : null;
  const filters = parsed?.filters ?? null;
  const results = useExternalSearch(parsed?.submitted ? filters : null);
  const capability = capabilities.data?.find(x => x.name === filters?.provider);
  return <div className={styles.screen}>
    <Link className={styles.back} href="/catalog"><IconArrowLeft size={18} aria-hidden="true" />Back to catalog</Link>
    <header className={styles.heading}><h1>Find your next title.</h1><p>Search a provider, explore the details,<br />and bring a title into the catalog.</p></header>
    {capabilities.isPending ? <LoadingState label="Loading providers…" /> : capabilities.isError ? <ProviderError error={capabilities.error} onRetry={() => void capabilities.refetch()} /> : parsed?.error ? <><ErrorState title={parsed.error} description="Return to discovery or browse the local catalog." /><Link className={styles.link} href="/catalog/discover">Reset discovery</Link></> : filters && <>
      <DiscoveryForm key={search.toString()} capabilities={capabilities.data} filters={filters} />
      {capability && <ProviderAttribution capability={capability} />}
      {!parsed?.submitted ? <section className={styles.guidance}><h2>Search a provider to find your next title.</h2><p>Preview first. Importing adds a shared catalog entry, not a personal library item.</p></section> : results.isPending ? <LoadingState label="Searching provider…" /> : results.isError ? <ProviderError error={results.error} onRetry={() => void results.refetch()} /> : <DiscoveryResults key={`${discoveryHref(filters)}:${results.dataUpdatedAt}`} data={results.data} filters={filters} />}
    </>}
  </div>;
}
