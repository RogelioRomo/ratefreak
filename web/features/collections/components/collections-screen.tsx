"use client";
import { Select } from "@/components/ui/select";
import Link from "next/link";
import { useEffect } from "react";
import { useSession } from "@/features/auth/hooks/use-session";
import { ApiError } from "@/shared/api/api-error";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/button";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { CollectionSession } from "./collection-session";
import { CollectionList } from "./collection-list";
import { collectionFilters, collectionHref } from "../query/filters";
import { useCollections } from "../hooks/use-collections";
import type { CollectionSessionState } from "../interfaces/component-props";
import styles from "./collections.module.css";
export function CollectionsScreen() {
  return <div className={styles.screen}><header className={styles.heading}><div><h1>Collections.</h1><p>Give your interests a place of their own.</p></div><Link className={styles.link} href="/collections/new">New collection</Link></header><CollectionSession returnTo="/collections">{state => <CollectionResults {...state}/>}</CollectionSession></div>;
}
function CollectionResults({viewer, active}: CollectionSessionState) {
  const search = useSearchParams();
  const router = useRouter();
  const filters = collectionFilters(new URLSearchParams(search.toString()));
  const query = useCollections(viewer, filters, active);
  const {refresh} = useSession();
  const unauthorized = query.error instanceof ApiError && query.error.status === 401;
  useEffect(() => {if(unauthorized) void refresh().catch(()=>{});}, [unauthorized,refresh]);
  if (!filters) return <><p role="alert">Invalid collection filters.</p><Link href="/collections">Reset filters</Link></>;
  return <>
    <p className={styles.hint}>All accessible includes public collections and, when signed in, your own private collections.</p>
    <form key={search.toString()} className={styles.filters} aria-label="Collection filters" onSubmit={event => {
      event.preventDefault(); const scope = new FormData(event.currentTarget).get("scope");
      router.push(collectionHref({...filters, scope: scope === "mine" ? "mine" : "all", page: 1}));
    }}><div><label htmlFor="collection-scope">Show collections</label><Select id="collection-scope" name="scope" defaultValue={filters.scope}><option value="all">All accessible</option><option value="mine">My collections</option></Select></div><Button type="submit">Apply</Button></form>
    {(unauthorized || (viewer === "anonymous" && filters.scope === "mine")) ? <p><Link className={styles.link} href="/login?returnTo=%2Fcollections">Log in to view your collections</Link></p>
      : query.isPending ? <LoadingState label="Loading collections…"/>
      : query.isError ? <ErrorState title="Collections unavailable." description="If requests are limited, wait before trying again." onRetry={() => void query.refetch()}/>
      : !query.data.items.length ? <section className={styles.empty}><h2>{filters.page > 1 ? "This page has no collections." : "No collections yet."}</h2>{filters.page > 1 ? <Link className={styles.link} href={collectionHref({...filters,page:1})}>Go to first page</Link> : <p>Create a collection to begin organizing your interests.</p>}</section>
      : <><p className={styles.summary}>{query.data.totalCount} {query.data.totalCount === 1 ? "collection" : "collections"} · Page {filters.page}</p><CollectionList items={query.data.items}/><nav className={styles.pagination} aria-label="Collection pages">{filters.page > 1 ? <Link href={collectionHref({...filters,page:filters.page-1})}>Previous</Link> : <span>Previous</span>}<span>Page {filters.page}</span>{filters.page < Math.ceil(query.data.totalCount/20) && filters.page < 10000 ? <Link href={collectionHref({...filters,page:filters.page+1})}>Next</Link> : <span>Next</span>}</nav></>}
  </>;
}
