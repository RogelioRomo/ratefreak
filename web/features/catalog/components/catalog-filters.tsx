"use client";
import { CatalogCategoryFilter } from "@/features/custom-catalog/components/catalog-category-filter";
import { useRouter } from "next/navigation";
import { IconSearch } from "@tabler/icons-react";
import { Button } from "@/components/ui/button";
import type { CatalogFiltersProps } from "../interfaces/component-props";
import { catalogHref, submittedFilters } from "../query/filters";
import styles from "./catalog-filters.module.css";

export function CatalogFilters({ filters }: CatalogFiltersProps) {
  const router = useRouter();
  return <form className={styles.form} role="search" aria-label="Catalog" onSubmit={event => {
    event.preventDefault();
    const values = new FormData(event.currentTarget);
    router.push(catalogHref(submittedFilters(String(values.get("q") ?? ""), "", String(values.get("categoryId") ?? ""))));
  }}>
    <div className={styles.field}><label htmlFor="catalog-title">Title</label>
      <input id="catalog-title" name="q" type="search" defaultValue={filters.q} maxLength={200} placeholder="Find an item by title" /></div>
    <CatalogCategoryFilter defaultValue={filters.categoryId} />
    <Button type="submit"><IconSearch size={18} aria-hidden="true" />Search catalog</Button>
  </form>;
}
