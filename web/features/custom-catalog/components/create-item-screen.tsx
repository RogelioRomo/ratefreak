"use client";
import { CollectionSession } from "@/features/collections/components/collection-session";
import { ItemEditor } from "./item-editor";
import styles from "./custom-catalog.module.css";
export function CreateItemScreen() {
  return <div className={styles.screen}><header className={styles.heading}><h1>Something worth rating.</h1><p>A flower, a book, a favourite place. Give it a name and category, then make it your own.</p></header><CollectionSession subject="catalog items" requireAccount returnTo="/catalog/new">{({ viewer, active }) => <ItemEditor viewer={viewer} active={active} />}</CollectionSession></div>;
}
