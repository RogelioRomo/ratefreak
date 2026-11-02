import Link from "next/link";
import type { CollectionListProps } from "../interfaces/component-props";
import styles from "./collections.module.css";
export function CollectionList({items}: CollectionListProps) {
  return <ul className={styles.list}>{items.map(item => <li key={item.id}><div><Link className={styles.title} href={`/collections/${item.id}`}>{item.name}</Link><p className={styles.hint}>{item.owner.displayName} · @{item.owner.handle}</p>{item.description && <p className={styles.description}>{item.description}</p>}</div><span className={styles.visibility}>{item.visibility === "private" ? "Private" : "Public"}</span></li>)}</ul>;
}
