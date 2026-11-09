"use client";
import { useQuery } from "@tanstack/react-query";
import { CollectionSession } from "@/features/collections/components/collection-session";
import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import { getManagedItem } from "../api/custom-catalog";
import type { ManagedItemIdProps, ManagedItemProps } from "../interfaces/custom-catalog";
import { ItemEditor } from "./item-editor";
import styles from "./custom-catalog.module.css";
export function ManageItemScreen({ id }: ManagedItemIdProps) { return <div className={styles.screen}><header className={styles.heading}><h1>Make it yours.</h1><p>Correct details, choose who can see it, or update its image.</p></header><CollectionSession subject="catalog items" requireAccount returnTo={`/catalog/${id}/edit`}>{({ viewer, active }) => <ManagedItem id={id} viewer={viewer} active={active} />}</CollectionSession></div>; }
function ManagedItem({ id, viewer, active }: ManagedItemProps) {
  const query = useQuery({ queryKey: ["custom-items", viewer, id], queryFn: ({ signal }) => getManagedItem(id, signal), enabled: active, retry: false });
  return query.isPending ? <LoadingState label="Loading your item…" /> : query.isError ? <ErrorState title="Item unavailable." description="This item may not belong to you, or your session may have expired." onRetry={() => void query.refetch()} /> : <ItemEditor key={query.data.id} initial={query.data} viewer={viewer} active={active} />;
}
