"use client";
import { useId, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Select } from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { getCategories } from "../api/custom-catalog";
import { useCatalogViewer } from "../hooks/use-catalog-viewer";
import type { CatalogCategoryFilterProps } from "../interfaces/custom-catalog";
import styles from "./custom-catalog.module.css";

export function CatalogCategoryFilter({ defaultValue = "", onChange }: CatalogCategoryFilterProps) {
  const id = useId();
  const { viewer, active } = useCatalogViewer();
  const [value, setValue] = useState(defaultValue);
  const query = useQuery({ queryKey: ["categories", viewer, "filters", ""], queryFn: ({ signal }) => getCategories("", signal), enabled: active, retry: false });

  return <div className={styles.field}>
    <label htmlFor={`${id}-category`}>Category</label>
    <Select id={`${id}-category`} name="categoryId" value={value} onChange={event => {
      setValue(event.target.value);
      onChange?.(event.target.value);
    }}>
      <option value="">All categories</option>
      {value && !query.data?.items.some(category => category.id === value) && <option value={value}>Selected category</option>}
      {query.isLoading && <option value="loading" disabled>Loading categories…</option>}
      {query.data?.items.map(category => <option key={category.id} value={category.id}>{category.name}{category.scope === "private" ? " (private)" : ""}</option>)}
    </Select>
    {query.isError && <>
      <p role="alert" className={styles.error}>Category filters could not be loaded.</p>
      <Button type="button" variant="secondary" onClick={() => void query.refetch()}>Retry categories</Button>
    </>}
  </div>;
}
