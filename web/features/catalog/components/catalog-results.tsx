import Link from "next/link";
import { IconArrowLeft, IconArrowRight } from "@tabler/icons-react";
import type { CatalogResultsProps } from "../interfaces/component-props";
import { catalogHref, mediaLabels } from "../query/filters";
import { MediaArtwork } from "./media-artwork";
import styles from "./catalog-results.module.css";

export function CatalogResults({ data, filters }: CatalogResultsProps) {
  const pages = Math.ceil(data.totalCount / filters.pageSize);
  if (!data.items.length) {
    const outOfRange = filters.page > 1;
    const filtered = !!(filters.q || filters.type);
    return <section className={styles.empty} aria-labelledby="catalog-empty">
      <h2 id="catalog-empty">{outOfRange ? "This page has no titles." : filtered ? "No matching titles." : "The catalog is waiting for its first title."}</h2>
      <p>{outOfRange ? "Return to the first page to see the available results." : filtered ? "Try another title or explore all media types." : "Imported movies, shows, albums and games will appear here."}</p>
      <Link href="/catalog/discover">Find titles from providers</Link>
      {(outOfRange || filtered) && <Link href={outOfRange ? catalogHref({ ...filters, page: 1 }) : "/catalog"}>{outOfRange ? "Go to first page" : "Browse all titles"}</Link>}
    </section>;
  }
  return <section aria-label="Catalog results">
    <div className={styles.summary}><p role="status">{data.totalCount} {data.totalCount === 1 ? "title" : "titles"}</p><p>Page {filters.page} of {pages}</p></div>
    <ul className={styles.results}>{data.items.map(item => <li key={item.id}>
      <Link className={styles.entry} aria-labelledby={`title-${item.id}`} href={`/catalog/${item.id}`}>
        <MediaArtwork reference={item.imageReference} title={item.title} type={item.type} />
        <h2 id={`title-${item.id}`}>{item.title}</h2>
      </Link>
      <p className={styles.meta}>{mediaLabels[item.type]}{item.releaseDate ? ` · ${item.releaseDate.slice(0, 4)}` : ""}</p>
    </li>)}</ul>
    <nav className={styles.pagination} aria-label="Catalog pages">
      {filters.page > 1 ? <Link href={catalogHref({ ...filters, page: filters.page - 1 })} rel="prev"><IconArrowLeft size={18} aria-hidden="true" />Previous</Link> : <span>Previous</span>}
      <span>Page {filters.page}</span>
      {filters.page < pages && filters.page < 10000 ? <Link href={catalogHref({ ...filters, page: filters.page + 1 })} rel="next">Next<IconArrowRight size={18} aria-hidden="true" /></Link> : <span>Next</span>}
    </nav>
  </section>;
}
