"use client";
import Link from 'next/link';
import { isMediaId } from '@/features/catalog/query/filters';
import { CollectionSession } from './collection-session';
import { CollectionWorkspace,CollectionUnavailable } from './collection-workspace';
import type { CollectionDetailProps } from '../interfaces/component-props';
import styles from './collections.module.css';
export function CollectionDetailScreen({id}:CollectionDetailProps){return <div className={styles.screen}><Link className={styles.link} href="/collections">Back to collections</Link>{!isMediaId(id)?<CollectionUnavailable/>:<CollectionSession returnTo={`/collections/${id.toLowerCase()}`}>{state=><CollectionWorkspace key={id.toLowerCase()} {...state} id={id.toLowerCase()}/>}</CollectionSession>}</div>;}
