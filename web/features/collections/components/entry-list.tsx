import Link from 'next/link';
import type { EntryListProps } from '../interfaces/contents-props';
import styles from './collections.module.css';
export function EntryList({collectionId,items}:EntryListProps){return <ul className={styles.list}>{items.map(item=><li key={item.id}><div><Link className={styles.title} href={`/collections/${collectionId.toLowerCase()}/items/${item.id}`}>{item.title}</Link><p className={styles.hint}>{item.mediaItemId?'Catalog title':'Custom entry'} · Position {item.position}</p>{item.mediaItemId&&<Link className={styles.link} href={`/catalog/${item.mediaItemId}`}>View catalog title</Link>}</div></li>)}</ul>;}
