'use client';
import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { Button } from '@/components/ui/button';
import { useSession } from '@/features/auth/hooks/use-session';
import { sessionQueryKey } from '@/features/auth/session/query-keys';
import type { AccountProfile } from '@/features/auth/interfaces/account-profile';
import type { ReportControlProps } from '../interfaces/reporting';
import { ReportForm } from './report-form';
import styles from './reporting.module.css';
export function ReportControl({target,available=true}: ReportControlProps) {
 const session=useSession(), client=useQueryClient();
 const cached=client.getQueryData<AccountProfile|null>(sessionQueryKey);
 const pathname=usePathname(), search=useSearchParams();
 const account=cached ?? session.account;
 const ready=available && !session.isLoading && !session.isTransitioning && !session.error;
 if (account?.id.toLowerCase() === target.authorId.toLowerCase()) return null;
 if (!account) return ready && session.account === null ? <p className={styles.control}><Link href={`/login?returnTo=${encodeURIComponent(`${pathname}${search?.toString() ? `?${search}` : ''}`)}`}>Log in to report {target.type}</Link></p> : null;
 return <AccountReport key={`${account.id}:${target.type}:${target.id}`} target={target} accountId={account.id} available={ready && session.account?.id === account.id}/>;
}
function AccountReport({target,accountId,available}: {target:ReportControlProps['target'];accountId:string;available:boolean}) {
 const [open,setOpen]=useState(false);const triggerId=useId();
 return <div className={styles.control} hidden={!available} inert={!available}>
  <Button id={triggerId} variant="secondary" disabled={!available} aria-expanded={open} onClick={()=>setOpen(true)}>Report {target.type}</Button>
  {open && <ReportForm target={target} accountId={accountId} available={available} onCancel={()=>{setOpen(false);document.getElementById(triggerId)?.focus();}}/>}
 </div>;
}
