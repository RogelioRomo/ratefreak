"use client";
import { Select } from "@/components/ui/select";
import { useRouter } from "next/navigation";
import { IconSearch } from "@tabler/icons-react";
import { Button } from "@/components/ui/button";
import type { CatalogFiltersProps } from "../interfaces/component-props";
import { catalogHref, mediaTypes, submittedFilters } from "../query/filters";
import styles from "./catalog-filters.module.css";

export function CatalogFilters({ filters }: CatalogFiltersProps) {
  const router = useRouter();
  return <form className={styles.form} role="search" aria-label="Catalog" onSubmit={event => {
    event.preventDefault();
    const values = new FormData(event.currentTarget);
    router.push(catalogHref(submittedFilters(String(values.get("q") ?? ""), String(values.get("type") ?? ""))));
  }}>
    <div className={styles.field}><label htmlFor="catalog-title">Title</label>
      <input id="catalog-title" name="q" type="search" defaultValue={filters.q} maxLength={200} placeholder="Find a film, record, show or game" /></div>
    <div className={styles.field}><label htmlFor="catalog-type">Media type</label>
      <Select id="catalog-type" name="type" defaultValue={filters.type}>
        <option value="">All types</option>
        {Object.entries(mediaTypes).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </Select></div>
    <Button type="submit"><IconSearch size={18} aria-hidden="true" />Search catalog</Button>
  </form>;
}
