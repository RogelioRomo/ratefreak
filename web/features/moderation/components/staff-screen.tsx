'use client';
import Link from 'next/link';
import {useSearchParams} from 'next/navigation';
import {useQueryClient} from '@tanstack/react-query';
import {useState,useSyncExternalStore} from 'react';
import {Button} from '@/components/ui/button';
import {Select} from '@/components/ui/select';
import {LoadingState} from '@/components/ui/loading-state';
import {getStaffAccounts} from '../api/staff-management';
import {staffAccountFilters,staffAccountParams} from '../query/staff-management';
import {accessStore} from '../query/access';
import {useStaff} from '../query/context';
import {useStaffQuery} from '../hooks/use-staff-query';
import {StaffNav} from './staff-nav';
import {StaffPaging} from './staff-paging';
import {StaffRoleEditor} from './staff-role-editor';
import type {StaffAccountFilters} from '../interfaces/staff-management';
import styles from './moderation.module.css';

export function StaffScreen(){
 const staff=useStaff(),client=useQueryClient(),store=accessStore(client),search=useSearchParams();
 const denied=useSyncExternalStore(store.subscribe,()=>store.denied(staff.accountId,'admin'),()=>0);
 let filters:StaffAccountFilters|null=null;try{filters=staffAccountFilters(new URLSearchParams(search.toString()));}catch{}
 return <div className={styles.screen}><StaffNav/><h1>Staff management.</h1>
  {!staff.active?<LoadingState label="Checking your session…"/>:denied?<section><h2>Administrator access required.</h2><p>Moderator appointments are managed by administrators. You can continue using the Reports link if you have moderator access.</p><Button onClick={()=>store.allow(staff.accountId,'admin')}>Check administrator access again</Button></section>:!filters?<p role="alert">Invalid account filters. <Link href="/moderation/staff">Reset filters</Link></p>:<AccountDirectory key={`${staff.accountId}:${search}`} filters={filters}/>}
 </div>;
}
function AccountDirectory({filters}:{filters:StaffAccountFilters}){
 const staff=useStaff(),client=useQueryClient(),[saved,setSaved]=useState(false);
 const query=useStaffQuery(['accounts',filters],signal=>getStaffAccounts(filters,signal),true,'admin');
 async function reload(){const result=await query.refetch();if(result.isError)throw result.error;}
 function onSaved(){setSaved(true);void client.invalidateQueries({queryKey:['staff',staff.accountId,'admin','accounts']});void client.invalidateQueries({queryKey:['staff',staff.accountId,'audit']});}
 return <>
  <p>Appoint moderators and review staff access. Administrator appointments are managed by the operator.</p>
  <form action="/moderation/staff" className={styles.filters}>
   <label htmlFor="staff-search">Search accounts</label><input id="staff-search" name="q" type="search" maxLength={100} defaultValue={filters.q} placeholder="Handle or display name"/>
   <label htmlFor="staff-filter">Show accounts</label><Select id="staff-filter" name="staffOnly" defaultValue={String(filters.staffOnly)}><option value="true">Staff only</option><option value="false">All accounts</option></Select>
   <Button type="submit">Search accounts</Button>
  </form>
  {saved&&<p role="status">Moderator appointment saved.</p>}
  {query.isError&&<div role="alert"><p>Account roles could not be loaded. Refresh before changing appointments.</p><Button variant="secondary" disabled={query.isFetching} onClick={()=>void query.refetch()}>Reload accounts</Button></div>}
  {query.isPending?<LoadingState label="Loading accounts…"/>:query.data&&<div hidden={query.isError} inert={query.isError} aria-busy={query.isFetching}>
   <p className={styles.meta}>{query.data.totalCount} {query.data.totalCount===1?'account':'accounts'} · Sorted by handle</p>
   {query.data.items.length?<ul className={styles.list}>{query.data.items.map(account=><li className={styles.row} key={account.id}>
    <h2>{account.displayName}</h2><p>@{account.handle}</p>{!account.isAdmin&&<p className={styles.meta}>{account.isModerator?'Moderator':'No staff role'}</p>}
    <div className={styles.roleAction}><StaffRoleEditor key={`${account.id}:${account.isAdmin}:${account.isModerator}:${query.dataUpdatedAt}`} account={account} available={staff.active&&!query.isFetching&&!query.isError} onSaved={onSaved} onReload={reload}/></div>
    <Link href={`/moderation/audit?targetType=account&targetId=${account.id}`} aria-label={`Audit history for @${account.handle}`}>View appointment history</Link>
   </li>)}</ul>:<p>{filters.page>1?'No accounts on this page.':filters.staffOnly?'No staff accounts match. Choose All accounts to find someone to appoint.':'No accounts match this search.'}</p>}
   <StaffPaging page={filters.page} total={query.data.totalCount} href={page=>`/moderation/staff?${staffAccountParams({...filters,page})}`}/>
  </div>}
 </>;
}
