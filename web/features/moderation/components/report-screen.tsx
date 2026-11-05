'use client';
import {useState} from 'react';
import Link from 'next/link';
import {useQueryClient} from '@tanstack/react-query';
import {isMediaId} from '@/features/catalog/query/filters';
import {LoadingState} from '@/components/ui/loading-state';
import {getReport} from '../api/moderation';
import {useStaffQuery} from '../hooks/use-staff-query';
import {useStaff} from '../query/context';
import {refreshModeration} from '../query/keys';
import type {StaffDetailProps} from '../interfaces/moderation';
import {StaffNav} from './staff-nav';
import {StaffFeedback} from './staff-feedback';
import {TargetContent} from './target-content';
import {ActionEditor} from './action-editor';
import styles from './moderation.module.css';
export function ReportScreen({id}:StaffDetailProps){
 const client=useQueryClient(),staff=useStaff(),valid=isMediaId(id);const [message,setMessage]=useState(''),[verifying,setVerifying]=useState(false);
 const query=useStaffQuery(['report',id],signal=>getReport(id,signal),valid);
 async function reload(){await query.refetch({throwOnError:true});setVerifying(false);}
 return <div className={styles.screen}><StaffNav/><h1>Report inspection.</h1>{message&&<p role="status">{message}</p>}{!valid?<p>Invalid report address.</p>:<><StaffFeedback error={query.error} onRetry={()=>void reload().catch(()=>{})}/>{query.isPending?<LoadingState label="Loading report…"/>:query.data&&<div className={styles.detail} hidden={query.isError} inert={query.isError}>
 <h2>Report {query.data.report.id}</h2><p>Status: {query.data.report.status}</p><p>Reported reason: {query.data.report.reason}</p><p>Reporter: {query.data.report.reporter?`${query.data.report.reporter.displayName} (@${query.data.report.reporter.handle})`:'Deleted account'}</p><p className={styles.meta}>Reported: {query.data.report.createdAt}</p>{query.data.details&&<p className={styles.body}>{query.data.details}</p>}
 {query.data.target?<><TargetContent key={query.data.target.id} target={query.data.target}/><Link href={`/moderation/${query.data.target.targetType==='review'?'reviews':'comments'}/${query.data.target.id}`}>Inspect visibility</Link></>:<p>Deleted content. Only dismissal is available.</p>}
 {query.data.report.status==='resolved'&&<dl className={styles.detail}><div><dt>Decision</dt><dd>{query.data.decision}</dd></div><div><dt>Resolution reason</dt><dd>{query.data.resolutionReason}</dd></div><div><dt>Resolved</dt><dd>{query.data.resolvedAt}</dd></div></dl>}
 <Link href={`/moderation/audit?targetType=${query.data.report.targetType}&targetId=${query.data.report.targetId}`}>Target audit history</Link>
 <ActionEditor target={query.data.target} report={query.data.report} available={staff.active&&!query.isError&&!query.isFetching&&!verifying} onReload={reload} onSaved={()=>{setMessage('Moderation action saved.');setVerifying(true);refreshModeration(client,staff.accountId);void reload().catch(()=>{});}}/>
 </div>}</>}</div>;
}
