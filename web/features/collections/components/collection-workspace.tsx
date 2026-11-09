"use client";
import Link from "next/link";
import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/shared/api/api-error";
import { Button } from "@/components/ui/button";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { useCollection } from "../hooks/use-collections";
import { useContentsActions } from "../hooks/use-contents-actions";
import { useEditorReload } from "../hooks/use-editor-reload";
import { getCollection } from "../api/collections";
import { clearUnavailableItems, getItems } from "../api/contents";
import { loadPageSnapshot } from "../api/snapshots";
import { collectionKeys } from "../query/keys";
import { itemsPage, itemsHref } from "../query/items-page";
import type { CollectionWorkspaceProps } from "../interfaces/contents-props";
import type { CollectionDetails } from "../interfaces/collections";
import type { CollectionItem } from "../interfaces/contents";
import { CollectionEditor } from "./collection-editor";
import { ListMembershipEditor } from "./list-membership-editor";
import { ListRemoval } from "./list-removal";
import { ContentsError } from "./contents-error";
import styles from "./collections.module.css";
export function CollectionUnavailable() { return <section className={styles.empty}><h1>Collection unavailable.</h1><p>This collection is missing or isn’t accessible to you.</p></section>; }
export function CollectionWorkspace({ id, viewer, accountId, active }: CollectionWorkspaceProps) {
  const params = useSearchParams(), page = itemsPage(new URLSearchParams(params.toString())); const query = useCollection(viewer, id, active); const client = useQueryClient(); const actions = useContentsActions(viewer, id, active); const reader = useEditorReload(active, id);
  const items = useQuery({ queryKey: ["collections", viewer, "list-items-v2", id, query.data?.collection.revision, page], queryFn: ({ signal }) => getItems(id, page!, signal), enabled: active && !!query.data && page !== null, retry: false });
  const [clearConfirmed, setClearConfirmed] = useState(false);
  const [editor, setEditor] = useState<{ kind: "metadata" | "add" | "reorder" | "remove"; details: CollectionDetails; item?: CollectionItem } | null>(null); const [saved, setSaved] = useState(""); const [gone, setGone] = useState(false);
  async function refresh(signal: AbortSignal) {
    const latest = await loadPageSnapshot(id, page ?? 1, signal); signal.throwIfAborted();
    client.setQueryData(collectionKeys.detail(viewer, id), latest.details); client.setQueryData(["collections", viewer, "list-items-v2", id, latest.details.collection.revision, page], latest.page);
    client.removeQueries({ predicate: q => q.queryKey[0] === "collections" && q.queryKey[1] !== viewer && (q.queryKey[3] === id || q.queryKey[2] === "list") }); void client.invalidateQueries({ predicate: q => q.queryKey[0] === "collections" && q.queryKey[2] === "list" });
  }
  async function open(kind: "metadata" | "add" | "reorder" | "remove", item?: CollectionItem) {
    if (!active || editor || actions.pending || actions.needsRefresh || reader.pending) return;
    actions.clearError(); setSaved(""); const latest = await reader.run(signal => getCollection(id, signal)); if (latest) setEditor({ kind, details: latest, item });
  }
  if (gone || query.error instanceof ApiError && query.error.status === 404) return <CollectionUnavailable />;
  if (page === null) return <ErrorState title="Invalid entry page." />;
  if (!query.data) return query.isError ? <ErrorState title="Collection unavailable." onRetry={() => void query.refetch()} /> : <LoadingState label="Loading collection…" />;
  const detail = query.data, collection = detail.collection, owner = accountId === collection.owner.id, locked = !active || !!editor || actions.pending || actions.needsRefresh || reader.pending;
  return <><header className={styles.heading}><div><h1>{collection.name}</h1><p>{collection.owner.displayName} (@{collection.owner.handle}) · {collection.visibility === "private" ? "Private" : "Public"}</p></div></header>{collection.description && <p className={styles.description}>{collection.description}</p>}
    {owner && <div className={`${styles.actions} ${styles.workspaceActions}`}><Button variant="secondary" disabled={locked} onClick={() => void open("metadata")}>Edit metadata</Button><Button disabled={locked} onClick={() => void open("add")}>Add item</Button></div>}
    {saved && <p role="status">{saved}</p>}<ContentsError error={reader.error} />
    {actions.needsRefresh && <section role="status"><p>Saved. Refresh needed before another change.</p><Button disabled={!active || actions.pending} variant="secondary" onClick={() => void actions.retryRefresh()}>Refresh collection</Button></section>}
    {editor?.kind === "metadata" && owner && <section className={styles.section}><h2>Edit collection</h2><CollectionEditor saved={editor.details} accountId={accountId!} active={active} onCancel={() => setEditor(null)} onUnavailable={() => setGone(true)} onSaved={updated => { client.setQueryData(collectionKeys.detail(viewer, id), updated); setEditor(null); setSaved("Collection saved."); }} /></section>}
    {editor && ["add", "reorder"].includes(editor.kind) && owner && <section className={styles.section}><h2>{editor.kind === "add" ? "Add item" : "Reorder item"}</h2><ListMembershipEditor details={editor.details} item={editor.item} active={active} actions={actions} refresh={refresh} onCancel={() => setEditor(null)} onSaved={() => { setEditor(null); setSaved("List updated."); }} /></section>}
    {editor?.kind === "remove" && editor.item && owner && <ListRemoval details={editor.details} item={editor.item} active={active} actions={actions} refresh={refresh} onCancel={() => setEditor(null)} onSaved={() => { setEditor(null); setSaved("Item removed from list."); }} />}
    <section className={styles.section} aria-label="List items"><h2>Items</h2>{items.isPending ? <LoadingState label="Loading entries…" /> : items.isError ? <ErrorState title="Entries unavailable." onRetry={() => void items.refetch()} /> : <>{!items.data.items.length && <p>No entries yet.</p>}<ul className={styles.list}>{items.data.items.map(item => <li key={item.id}><div><Link className={styles.title} href={`/catalog/${item.mediaItemId}`}>{item.media.title}</Link><p className={styles.hint}>Position {item.position}</p></div>{owner && <div className={styles.actions}><Button variant="secondary" disabled={locked} onClick={() => void open("reorder", item)}>Reorder {item.media.title}</Button><Button variant="secondary" disabled={locked} onClick={() => void open("remove", item)}>Remove {item.media.title}</Button></div>}</li>)}</ul><nav className={styles.pagination} aria-label="Entry pages">{page > 1 ? <Link href={itemsHref(id, page - 1)}>Previous</Link> : <span>Previous</span>}<span>Page {page}</span>{page * 20 < items.data.totalCount && page < 10000 ? <Link href={itemsHref(id, page + 1)}>Next</Link> : <span>Next</span>}</nav></>}</section>
    {owner && <section className={styles.section} aria-label="Unavailable memberships"><h2>Unavailable items</h2><p>Items that are private to someone else, archived or hidden stay in this list until you choose to remove them. Their details are unavailable.</p><label className={styles.check}><input type="checkbox" checked={clearConfirmed} disabled={locked} onChange={e => setClearConfirmed(e.target.checked)} />Remove all unavailable memberships from this list</label><ContentsError error={actions.error} /><Button type="button" variant="secondary" disabled={locked || !clearConfirmed || actions.outcomeUnknown || !!actions.error} onClick={async () => { const result = await actions.execute(signal => clearUnavailableItems(id, collection.revision, signal), refresh); if (result) { setClearConfirmed(false); setSaved("Unavailable memberships removed. Catalog items and contributions remain."); } }}>Remove unavailable memberships</Button></section>}
    <p className={styles.hint}>Each entry refers to the same catalog item and personal rating wherever you add it. Public list entries vary with item access.</p></>;
}
