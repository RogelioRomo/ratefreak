'use client';
import Link from 'next/link';
import {useSearchParams} from 'next/navigation';
import {Button} from '@/components/ui/button';
import {Select} from '@/components/ui/select';
import {LoadingState} from '@/components/ui/loading-state';
import {getAudit} from '../api/moderation';
import {staffFilters,staffParams} from '../query/filters';
import {useStaffQuery} from '../hooks/use-staff-query';
import {StaffNav} from './staff-nav';
import {StaffFeedback} from './staff-feedback';
import {StaffPaging} from './staff-paging';
import styles from './moderation.module.css';
export function AuditScreen(){
 const search=useSearchParams();let filters=null;try{const params=new URLSearchParams(search.toString());filters=staffFilters(params,'audit');}catch{}
 const query=useStaffQuery(['audit',filters],signal=>getAudit(filters!,signal),!!filters);
 return <div className={styles.screen}><StaffNav/><h1>Moderation audit.</h1>{!filters?<p role="alert">Invalid audit filters. <Link href="/moderation/audit">Reset filters</Link></p>:<>
 <form action="/moderation/audit" className={styles.filters} key={search.toString()}><label htmlFor="audit-type">Target type</label><Select id="audit-type" name="targetType" defaultValue={filters.targetType??''}><option value="">All types</option><option value="review">Review</option><option value="comment">Comment</option><option value="account">Account</option></Select><label htmlFor="audit-id">Target ID</label><input id="audit-id" name="targetId" defaultValue={filters.targetId??''} placeholder="Optional UUID"/><Button type="submit">Apply filters</Button></form>
 <StaffFeedback error={query.error} onRetry={()=>void query.refetch()}/>{query.isPending?<LoadingState label="Loading audit…"/>:query.data&&<div hidden={query.isError} inert={query.isError}><p>Events: {query.data.totalCount} · Newest first</p>{query.data.items.length?<ul className={styles.list}>{query.data.items.map(entry=><li key={entry.id} className={styles.row}><h2>{entry.action}</h2><time dateTime={entry.createdAt}>{entry.createdAt}</time><p>Actor: {entry.actorKind==='operator'?'Operator':entry.actorId??'Deleted account'}</p><p>Target: {entry.targetType} {entry.targetId}</p><p className={styles.body}>{entry.reason}</p><p>Before: {entry.before===null?'Not recorded':String(entry.before)} · After: {entry.after===null?'Not recorded':String(entry.after)}</p>{entry.reviewedUpdatedAt&&<p>Reviewed version: {entry.reviewedUpdatedAt}</p>}{entry.reportId&&<Link href={`/moderation/reports/${entry.reportId}`}>Related report</Link>}</li>)}</ul>:<p>{filters.page>1?'No audit events on this page.':'No audit events match these filters.'}</p>}<StaffPaging page={filters.page} total={query.data.totalCount} href={page=>`/moderation/audit?${staffParams({...filters,page})}`}/></div>}
 </>}</div>;
}
