"use client";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { isMediaId } from "@/features/catalog/query/filters";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { CollectionSession } from "./collection-session";
import { useEntryContents } from "../hooks/use-collection-contents";
import type { EntryDetailProps } from "../interfaces/contents-props";
import type { CollectionSessionState } from "../interfaces/component-props";
import styles from "./collections.module.css";
export function EntryDetailScreen({ collectionId, itemId }: EntryDetailProps) { const params = useSearchParams(); return <div className={styles.screen}><Link href={`/collections/${collectionId}`}>Back to collection</Link>{!isMediaId(collectionId) || !isMediaId(itemId) || params.size ? <ErrorState title="Entry unavailable." /> : <CollectionSession returnTo={`/collections/${collectionId}`}>{state => <EntryContent {...state} collectionId={collectionId} itemId={itemId} />}</CollectionSession>}</div>; }
function EntryContent({ collectionId, itemId, viewer, active }: EntryDetailProps & CollectionSessionState) {
 const query = useEntryContents(viewer, collectionId, itemId, active);
 return query.isPending ? <LoadingState label="Loading entry…" /> : query.isError ? <ErrorState title="Entry unavailable." onRetry={() => void query.refetch()} /> : <><header className={styles.heading}><h1>{query.data.item.media.title}</h1></header><p>In {query.data.details.collection.name} · Position {query.data.item.position}</p><Link className={styles.link} href={`/catalog/${query.data.item.mediaItemId}`}>View catalog item and rating</Link><p className={styles.hint}>Reorder or remove this membership from its collection.</p></>;
}
