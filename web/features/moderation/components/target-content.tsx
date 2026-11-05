'use client';
import {useState} from 'react';
import Link from 'next/link';
import {Button} from '@/components/ui/button';
import type {ModerationTarget} from '../interfaces/moderation';
import styles from './moderation.module.css';
export function TargetContent({target}:{target:ModerationTarget}){
 const [reveal,setReveal]=useState(false);
 return <section className={styles.detail} aria-label="Current content"><h2>Current {target.targetType}</h2><p>This is the current content, not a snapshot of the original report.</p><p>{target.author?<Link href={`/users/${target.author.handle}`}>{target.author.displayName} (@{target.author.handle})</Link>:'Deleted account'}</p><p>Visibility setting: {target.hidden?'Hidden':'Not hidden'}</p>{target.targetType==='comment'&&<p>A comment may remain unavailable publicly when its parent review is hidden.</p>}<p className={styles.meta}>Inspected version: {target.updatedAt}</p>{target.isSpoiler&&<Button variant="secondary" aria-expanded={reveal} onClick={()=>setReveal(!reveal)}>{reveal?'Hide content spoilers':'Show content spoilers'}</Button>}{(!target.isSpoiler||reveal)&&<p className={styles.body}>{target.body}</p>}</section>;
}
