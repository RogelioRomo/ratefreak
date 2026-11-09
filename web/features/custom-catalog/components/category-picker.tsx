"use client";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Select } from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { getCategories, createCategory } from "../api/custom-catalog";
import type { CategoryPage, CategoryPickerProps } from "../interfaces/custom-catalog";
import styles from "./custom-catalog.module.css";
export function CategoryPicker({ viewer, value, onChange, selected, disabled }: CategoryPickerProps) {
  const client = useQueryClient();
  const [mode, setMode] = useState("existing");
  const [name, setName] = useState("");
  const [scope, setScope] = useState<"private" | "public">("private");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  const categories = useQuery({ queryKey: ["categories", viewer, ""], queryFn: ({ signal }) => getCategories("", signal), retry: false });
  const options = categories.data?.items ?? []; const withSelected = selected && !options.some(c => c.id === selected.id) ? [selected, ...options] : options;
  return <section className={styles.section} aria-label="Item category">
    <div className={styles.field}>
      <label htmlFor="category-option">Category option</label>
      <Select id="category-option" value={mode} disabled={disabled || pending} onChange={event => {
        setMode(event.target.value);
        onChange("");
        setError("");
      }}>
        <option value="existing">Select an existing category</option>
        <option value="new">Create a new category</option>
      </Select>
    </div>
    {mode === "existing" ? <>
      <div className={styles.field}><label htmlFor="item-category">Category</label><Select id="item-category" value={value} onChange={e => onChange(e.target.value)} disabled={disabled || categories.isPending}><option value="">Choose a category</option>{withSelected.map(c => <option key={c.id} value={c.id}>{c.name}{c.scope === "private" ? " (private)" : ""}</option>)}</Select></div>
      {categories.isError && <p role="alert" className={styles.error}>Categories could not be loaded. <button type="button" disabled={disabled} onClick={() => void categories.refetch()}>Retry categories</button></p>}
    </> : <div className={styles.form}>
      <div className={styles.field}><label htmlFor="new-category-name">New category name</label><input id="new-category-name" value={name} maxLength={100} onChange={e => setName(e.target.value)} disabled={disabled || pending} /></div>
      <div className={styles.field}><label htmlFor="new-category-scope">Category visibility</label><Select id="new-category-scope" value={scope} onChange={e => setScope(e.target.value as "private" | "public")} disabled={disabled || pending}><option value="private">Private — only you</option><option value="public">Public — available to everyone</option></Select></div>
      <p className={styles.hint}>Public categories are shared immediately. Publishing an item requires a public category.</p>
      <Button type="button" variant="secondary" disabled={disabled || pending || !name.trim()} onClick={async () => {
        setPending(true);
        setError("");
        try {
          const category = await createCategory(name.trim(), scope);
          client.setQueryData<CategoryPage>(["categories", viewer, ""], current => ({
            page: current?.page ?? 1,
            pageSize: current?.pageSize ?? 100,
            totalCount: (current?.totalCount ?? 0) + (current?.items.some(c => c.id === category.id) ? 0 : 1),
            items: [...(current?.items ?? []).filter(c => c.id !== category.id), category],
          }));
          void client.invalidateQueries({ queryKey: ["categories", viewer], refetchType: "none" });
          onChange(category.id);
          setMode("existing");
          setName("");
        } catch {
          setError("Category could not be created. Check the name or select an existing category before trying again.");
        } finally {
          setPending(false);
        }
      }}>{pending ? "Creating category…" : "Create category"}</Button>
      {error && <p role="alert" className={styles.error}>{error}</p>}
    </div>}
  </section>;
}
