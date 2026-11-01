"use client";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ApiError } from "@/shared/api/api-error";
import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import { useCatalog } from "../hooks/use-catalog";
import { parseFilters } from "../query/filters";
import { CatalogFilters } from "./catalog-filters";
import { CatalogResults } from "./catalog-results";
import styles from "./catalog-screen.module.css";

export function CatalogScreen() {
  const search = useSearchParams();
  const filters = parseFilters(new URLSearchParams(search.toString()));
  const catalog = useCatalog(filters);
  const limited = catalog.error instanceof ApiError && catalog.error.status === 429;
  return <div className={styles.screen}>
    <header className={styles.heading}><h1>The catalog.</h1><p>Films, shows, records and games.<br />Explore what’s made its way into RateFreak.</p></header>
    <Link className={styles.recovery} href="/catalog/discover">Find titles from providers</Link>
    {!filters ? <><ErrorState title="Invalid catalog filters." description="This link contains unsupported or repeated filters. Reset them to browse the catalog." /><Link className={styles.recovery} href="/catalog">Reset filters</Link></> : <>
      <CatalogFilters key={search.toString()} filters={filters} />
      {catalog.isPending ? <LoadingState label="Loading catalog…" /> : catalog.isError ? <ErrorState title={limited ? "Too many requests." : "Catalog unavailable."} description={limited ? "Please wait a moment before trying again." : "We couldn’t load these titles. Please try again."} onRetry={() => void catalog.refetch()} /> : <CatalogResults data={catalog.data} filters={filters} />}
    </>}
  </div>;
}
