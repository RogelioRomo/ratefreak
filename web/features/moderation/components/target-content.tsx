'use client';
import {useState} from 'react';
import Link from 'next/link';
import {Button} from '@/components/ui/button';
import type {ModerationTarget} from '../interfaces/moderation';
import styles from './moderation.module.css';
export function TargetContent({target}:{target:ModerationTarget}){
 const [reveal,setReveal]=useState(false);
 return <section className={styles.detail} aria-label="Current content"><h2>Current {target.targetType}</h2><p>This is the current content, not a snapshot of the original report.</p><p>{target.author?<Link href={`/users/${target.author.handle}`}>{target.author.displayName} (@{target.author.handle})</Link>:'Deleted account'}</p>{target.targetType==='profile'?<><dl className={styles.detail}><div><dt>Display name</dt><dd>{target.body}</dd></div><div><dt>Bio</dt><dd>{target.description||'No bio.'}</dd></div></dl><p>Restoring publishes the current saved name and bio. Owner edits remain hidden until staff restore the profile text.</p></>:target.description && <p className={styles.body}>{target.description}</p>}{target.imageReference && /^\/api\/v1\/moderation\/(?:reports|media)\/[0-9a-f-]{36}\/artwork\/[0-9a-f-]{36}$/i.test(target.imageReference) &&
 // eslint-disable-next-line @next/next/no-img-element -- authenticated no-store staff image; never use the public image optimizer.
 <img src={target.imageReference} alt="Current reported item artwork" style={{maxWidth:"100%",maxHeight:"24rem",objectFit:"contain"}} referrerPolicy="no-referrer" />}
 <p>Visibility setting: {target.hidden?'Hidden':'Not hidden'}</p>{target.targetType==='comment'&&<p>A comment may remain unavailable publicly when its parent review is hidden.</p>}<p className={styles.meta}>Inspected version: {target.updatedAt}</p>{target.isSpoiler&&<Button variant="secondary" aria-expanded={reveal} onClick={()=>setReveal(!reveal)}>{reveal?'Hide content spoilers':'Show content spoilers'}</Button>}{target.targetType!=='profile'&&(!target.isSpoiler||reveal)&&<p className={styles.body}>{target.body}</p>}</section>;
}
