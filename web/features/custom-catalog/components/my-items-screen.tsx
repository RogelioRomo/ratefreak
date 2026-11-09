"use client";
import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { apiRequest } from "@/shared/api/request";
import type { CatalogPage } from "@/features/catalog/interfaces/catalog-page";
import { CollectionSession } from "@/features/collections/components/collection-session";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import type { OwnItemsProps } from "../interfaces/custom-catalog";
import { Button } from "@/components/ui/button";
import styles from "./custom-catalog.module.css";
import pageStyles from "./my-items-screen.module.css";
export function MyItemsScreen() { return <div className={styles.screen}><header className={styles.heading}><h1>Your corner of the catalog.</h1><p>Manage everything you’ve created, including private and archived items.</p><nav className={pageStyles.links} aria-label="Your item actions"><Link className={styles.link} href="/catalog/new">Create an item</Link><Link className={styles.link} href="/catalog/categories">Manage categories</Link></nav></header><CollectionSession subject="catalog items" requireAccount returnTo="/catalog/mine">{({ viewer, active }) => <OwnItems viewer={viewer} active={active} />}</CollectionSession></div>; }
function OwnItems({ viewer, active }: OwnItemsProps) {
  const [page, setPage] = useState(1); const query = useQuery({ queryKey: ["custom-items", viewer, "list", page], queryFn: ({ signal }) => apiRequest<CatalogPage>(`media?ownOnly=true&page=${page}`, { signal }), enabled: active, retry: false });
  if (query.isPending) return <LoadingState label="Loading your items…" />;
  if (query.isError) return <ErrorState title="Your items are unavailable." onRetry={() => void query.refetch()} />;
  return <><ul className={styles.list}>{query.data.items.map(item => <li key={item.id}><span>{item.title}</span><Link href={`/catalog/${item.id}/edit`}>Manage {item.title}</Link></li>)}</ul>{!query.data.items.length && <p>You haven’t created any items yet.</p>}<nav className={`${styles.actions} ${pageStyles.pagination}`} aria-label="Your item pages"><Button variant="secondary" disabled={page === 1} onClick={() => setPage(page - 1)}>Previous</Button><span>Page {page}</span><Button variant="secondary" disabled={page * query.data.pageSize >= query.data.totalCount} onClick={() => setPage(page + 1)}>Next</Button></nav></>;
}
