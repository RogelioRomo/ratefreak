"use client";
import Link from "next/link";
import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/shared/api/api-error";
import { useCollectionWrite } from "@/features/collections/hooks/use-collection-write";
import { MediaArtwork } from "@/features/catalog/components/media-artwork";
import { getManagedItem, createItem, updateItem, uploadImage, removeImage, changeItemState } from "../api/custom-catalog";
import type { CustomItem, ItemEditorProps } from "../interfaces/custom-catalog";
import { CategoryPicker } from "./category-picker";
import styles from "./custom-catalog.module.css";
export function ItemEditor({ initial, viewer, active }: ItemEditorProps) {
  const [saved, setSaved] = useState(initial); const [title, setTitle] = useState(initial?.title ?? ""); const [description, setDescription] = useState(initial?.description ?? ""); const [categoryId, setCategoryId] = useState(initial?.category.id ?? ""); const [visibility, setVisibility] = useState<"private" | "public">("private"); const [file, setFile] = useState<File | null>(null); const [validation, setValidation] = useState("");
  const write = useCollectionWrite(); const client = useQueryClient(); const disabled = write.pending || !active;
  const needsReload = write.outcomeUnknown || write.error instanceof ApiError && write.error.status === 409;
  const run = async (operation: (signal: AbortSignal) => Promise<CustomItem>) => {
    const result = await write.run(async signal => { const item = await operation(signal); signal.throwIfAborted(); setSaved(item); void client.invalidateQueries({ predicate: q => ["catalog", "categories", "personal", "community", "collections", "review-discussion", "comments", "custom-items"].includes(String(q.queryKey[0])) }); return item; });
    return result;
  };
  const input = { title: title.trim(), description: description.trim() || null, categoryId, visibility };
  return <div className={styles.form}>
    <form className={styles.form} aria-label={saved ? "Edit item" : "Create item"} onSubmit={async event => { event.preventDefault(); if (disabled || needsReload) return; setValidation(""); if (!input.title || !categoryId) { setValidation("Enter a title and choose a category."); return; } await run(signal => saved ? updateItem(saved.id, saved.revision, input, signal) : createItem(input, signal)); }}>
      {saved && <p className={styles.hint}>Correct this item’s details. For a different subject, <Link href="/catalog/new">create another item</Link> so existing ratings keep their meaning.</p>}
      <div className={styles.field}><label htmlFor="custom-title">Title</label><input id="custom-title" value={title} onChange={e => setTitle(e.target.value)} maxLength={300} required disabled={disabled} /></div>
      <div className={styles.field}><label htmlFor="custom-description">Description</label><textarea id="custom-description" value={description} onChange={e => setDescription(e.target.value)} maxLength={20000} rows={5} disabled={disabled} /></div>
      <CategoryPicker viewer={viewer} value={categoryId} onChange={setCategoryId} selected={saved?.category} disabled={disabled} />
      {!saved && <div className={styles.field}><label htmlFor="item-visibility">Item visibility</label><Select id="item-visibility" value={visibility} onChange={e => setVisibility(e.target.value as "private" | "public")} disabled={disabled}><option value="private">Private — only you</option><option value="public">Public — publish to the catalog</option></Select></div>}
      <div className={styles.actions}><Button type="submit" disabled={disabled || needsReload}>{write.pending ? "Saving…" : saved ? "Save changes" : "Create item"}</Button>{saved && !saved.isArchived && <Link href={`/catalog/${saved.id}`}>View item</Link>}<Link href="/catalog/mine">My items</Link></div>
    </form>
    {validation && <p role="alert" className={styles.error}>{validation}</p>}
    {write.error && <p role="alert" className={styles.error}>{write.error instanceof ApiError && write.error.status === 409 ? "This item changed. Reload its latest version before saving again." : "The change could not be saved. Your entries are still here; check the category, image or connection before trying again."}</p>}
    {needsReload && <p role="alert">{write.outcomeUnknown && <>The outcome is unknown. <Link href="/catalog/mine">Check My items</Link> before trying again. </>}{saved && <Button type="button" variant="secondary" disabled={disabled} onClick={async () => { try { const current = await getManagedItem(saved.id); setSaved(current); write.clearError(); } catch { setValidation("The saved item could not be checked. Keep this page open and try reloading it again."); } }}>Reload saved item</Button>}</p>}
    {saved && <>
      <p role="status">Item saved · {saved.isArchived ? "Archived" : saved.visibility === "private" ? "Private" : "Public"}</p>
      <section className={styles.section} aria-labelledby="item-image-heading"><h2 id="item-image-heading">Item image</h2>
        {saved.imageReference && <div className={styles.preview}><MediaArtwork reference={saved.imageReference} title={saved.title} type="custom" /></div>}
        <div className={styles.field}><label htmlFor="item-image">Item image</label><input id="item-image" type="file" accept="image/jpeg,image/png,image/webp" disabled={disabled} onChange={e => { const next = e.target.files?.[0] ?? null; if (next && next.size > 5 * 1024 * 1024) { setValidation("Choose an image no larger than 5 MiB."); setFile(null); } else { setValidation(""); setFile(next); } }} /></div>
        <p className={styles.hint}>One JPEG, PNG or WebP image, up to 5 MiB. It follows this item’s visibility.</p>
        <div className={styles.actions}><Button type="button" disabled={disabled || !file || needsReload} onClick={() => void run(signal => uploadImage(saved, file!, signal))}>Upload image</Button>{saved.imageReference && <Button type="button" variant="secondary" disabled={disabled || needsReload} onClick={() => void run(signal => removeImage(saved, signal))}>Remove image</Button>}</div>
      </section>
      <section className={styles.section} aria-labelledby="item-access-heading"><h2 id="item-access-heading">Visibility and availability</h2><p className={styles.hint}>Making an item private hides it from everyone else and preserves their contributions. Archive keeps the item and its ratings; restoring makes it private.</p><div className={styles.actions}>
        {!saved.isArchived && <Button type="button" variant="secondary" disabled={disabled || needsReload} onClick={() => void run(signal => changeItemState(saved, saved.visibility === "public" ? "unpublish" : "publish", signal))}>{saved.visibility === "public" ? "Make private" : "Publish item"}</Button>}
        <Button type="button" variant="secondary" disabled={disabled || needsReload} onClick={() => void run(signal => changeItemState(saved, saved.isArchived ? "restore" : "archive", signal))}>{saved.isArchived ? "Restore item" : "Archive item"}</Button>
      </div></section>
    </>}
  </div>;
}
