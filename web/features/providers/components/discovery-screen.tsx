"use client";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { IconArrowLeft, IconArrowRight } from "@tabler/icons-react";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { MediaArtwork } from "@/features/catalog/components/media-artwork";
import { mediaLabels } from "@/features/catalog/query/filters";
import { useProviders, useExternalSearch } from "../hooks/use-providers";
import { parseDiscovery, discoveryHref } from "../query/filters";
import { previewHref, providerIdentity } from "../query/identity";
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
      {!parsed?.submitted ? <section className={styles.guidance}><h2>Search a provider to find your next title.</h2><p>Preview first. Importing adds a shared catalog entry, not a personal library item.</p></section> : results.isPending ? <LoadingState label="Searching provider…" /> : results.isError ? <ProviderError error={results.error} onRetry={() => void results.refetch()} /> : <section aria-label="Provider results">
        <div className={styles.summary}><p role="status">{results.data.items.length} {results.data.items.length === 1 ? "result" : "results"} on this page</p><p>Page {filters.page}</p></div>
        {!results.data.items.length ? <div className={styles.guidance}><h2>No titles on this page.</h2><p>Try another search{filters.page > 1 ? " or return to the first page." : "."}</p>{filters.page > 1 && <Link className={styles.link} href={discoveryHref({ ...filters, page: 1 })}>Go to first page</Link>}</div> : <ul className={styles.grid}>{results.data.items.map(item => {
          const identity = providerIdentity(item.provider, item.type, item.externalId);
          return identity ? <li key={`${item.provider}-${item.type}-${item.externalId}`}><Link className={styles.entry} href={previewHref(identity)} aria-labelledby={`external-${item.externalId}`}><MediaArtwork reference={item.imageReference} title={item.title} type={item.type} /><h2 id={`external-${item.externalId}`}>{item.title}</h2></Link><p className={styles.meta}>{mediaLabels[item.type]}{item.releaseDate ? ` · ${item.releaseDate.slice(0, 4)}` : ""}</p></li> : null;
        })}</ul>}
        <nav className={styles.pagination} aria-label="Provider pages">{filters.page > 1 ? <Link href={discoveryHref({ ...filters, page: filters.page - 1 })}><IconArrowLeft size={18} aria-hidden="true" />Previous</Link> : <span>Previous</span>}<span>Page {filters.page}</span>{results.data.items.length > 0 && filters.page < 500 ? <Link href={discoveryHref({ ...filters, page: filters.page + 1 })}>Next<IconArrowRight size={18} aria-hidden="true" /></Link> : <span>Next</span>}</nav>
      </section>}
    </>}
  </div>;
}
