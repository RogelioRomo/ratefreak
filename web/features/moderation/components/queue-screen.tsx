'use client';
import Link from 'next/link';
import {useSearchParams} from 'next/navigation';
import {Button} from '@/components/ui/button';
import {Select} from '@/components/ui/select';
import {LoadingState} from '@/components/ui/loading-state';
import {getReports} from '../api/moderation';
import {staffFilters,staffParams} from '../query/filters';
import {useStaffQuery} from '../hooks/use-staff-query';
import {StaffNav} from './staff-nav';
import {StaffFeedback} from './staff-feedback';
import {StaffPaging} from './staff-paging';
import styles from './moderation.module.css';
export function QueueScreen(){
 const search=useSearchParams();let filters=null;try{filters=staffFilters(new URLSearchParams(search.toString()),'reports');}catch{}
 const query=useStaffQuery(['reports',filters],signal=>getReports(filters!,signal),!!filters);
 return <div className={styles.screen}><StaffNav/><h1>Content reports.</h1>{!filters?<p role="alert">Invalid report filters. <Link href="/moderation">Reset filters</Link></p>:<>
 <form action="/moderation" className={styles.filters}><label htmlFor="report-status">Report status</label><Select key={filters.status} id="report-status" name="status" defaultValue={filters.status}><option value="open">Open</option><option value="resolved">Resolved</option></Select><Button type="submit">Apply filters</Button></form>
 <StaffFeedback error={query.error} onRetry={()=>void query.refetch()}/>{query.isPending?<LoadingState label="Loading reports…"/>:query.data&&<div hidden={query.isError} inert={query.isError}><p>Reports: {query.data.totalCount} · Oldest first</p>{query.data.items.length?<ul className={styles.list}>{query.data.items.map(report=><li key={report.id} className={styles.row}><h2><Link href={`/moderation/reports/${report.id}`}>Open report {report.id}</Link></h2><p>{report.targetType}: {report.targetId}</p><p>Reason: {report.reason}</p><p>{report.reporter?`${report.reporter.displayName} (@${report.reporter.handle})`:'Deleted account'}</p><time dateTime={report.createdAt}>{report.createdAt}</time></li>)}</ul>:<p>{filters.page>1?'No reports on this page.':'No reports with this status.'}</p>}<StaffPaging page={filters.page} total={query.data.totalCount} href={page=>`/moderation?${staffParams({...filters,page})}`}/></div>}
 </>}</div>;
}
