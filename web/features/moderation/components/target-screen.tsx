'use client';
import {useState} from 'react';
import Link from 'next/link';
import {useQueryClient} from '@tanstack/react-query';
import {LoadingState} from '@/components/ui/loading-state';
import {isMediaId} from '@/features/catalog/query/filters';
import {getTarget} from '../api/moderation';
import {useStaffQuery} from '../hooks/use-staff-query';
import {useStaff} from '../query/context';
import {refreshModeration} from '../query/keys';
import type {StaffTargetProps} from '../interfaces/moderation';
import {StaffNav} from './staff-nav';
import {StaffFeedback} from './staff-feedback';
import {TargetContent} from './target-content';
import {ActionEditor} from './action-editor';
import styles from './moderation.module.css';
export function TargetScreen({id,segment}:StaffTargetProps){
 const client=useQueryClient(),staff=useStaff(),valid=isMediaId(id);const [message,setMessage]=useState(''),[verifying,setVerifying]=useState(false);
 const query=useStaffQuery(['target',segment,id],signal=>getTarget(segment,id,signal),valid);
 async function reload(){await query.refetch({throwOnError:true});setVerifying(false);}
 return <div className={styles.screen}><StaffNav/><h1>Content visibility.</h1>{message&&<p role="status">{message}</p>}{!valid?<p>Invalid content address.</p>:<><StaffFeedback error={query.error} onRetry={()=>void reload().catch(()=>{})}/>{query.isPending?<LoadingState label="Loading content…"/>:query.data&&<div hidden={query.isError} inert={query.isError}><TargetContent key={query.data.id} target={query.data}/><Link href={`/moderation/audit?targetType=${query.data.targetType}&targetId=${id}`}>Target audit history</Link><ActionEditor target={query.data} segment={segment} available={staff.active&&!query.isError&&!query.isFetching&&!verifying} onReload={reload} onSaved={()=>{setMessage('Moderation action saved.');setVerifying(true);refreshModeration(client,staff.accountId);void reload().catch(()=>{});}}/></div>}</>}</div>;
}
