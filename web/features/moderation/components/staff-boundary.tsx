'use client';
import Link from 'next/link';
import {usePathname,useSearchParams} from 'next/navigation';
import {useQueryClient} from '@tanstack/react-query';
import {useEffect,useLayoutEffect,useRef,useState,useSyncExternalStore} from 'react';
import {useSession} from '@/features/auth/hooks/use-session';
import {sessionQueryKey} from '@/features/auth/session/query-keys';
import type {AccountProfile} from '@/features/auth/interfaces/account-profile';
import {Button} from '@/components/ui/button';
import {LoadingState} from '@/components/ui/loading-state';
import {StaffContext} from '../query/context';
import {accessStore} from '../query/access';
import {getReports} from '../api/moderation';
import type {StaffBoundaryProps} from '../interfaces/moderation';
import styles from './moderation.module.css';
export function StaffBoundary({children}:StaffBoundaryProps){
 const session=useSession(),client=useQueryClient();const path=usePathname(),params=useSearchParams();
 const cached=client.getQueryData<AccountProfile|null>(sessionQueryKey),account=cached??session.account;
 if(session.account===null)return <div className={styles.screen}><h1>Moderation tools.</h1><Link href={`/login?returnTo=${encodeURIComponent(`${path}${params?.toString()?`?${params}`:''}`)}`}>Log in to continue</Link></div>;
 if(!account)return <LoadingState label="Checking your session…"/>;
 return <AccountBoundary key={account.id} accountId={account.id} active={session.account?.id===account.id&&!session.error&&!session.isLoading&&!session.isTransitioning}>{children}</AccountBoundary>;
}
function AccountBoundary({accountId,active,children}:StaffBoundaryProps&{accountId:string;active:boolean}){
 const client=useQueryClient(),store=accessStore(client),denied=useSyncExternalStore(store.subscribe,()=>store.denied(accountId),()=>0);
 const request=useRef<AbortController|null>(null),latest=useRef(active);const [busy,setBusy]=useState(false),[error,setError]=useState('');
 useLayoutEffect(()=>{latest.current=active;if(!active)request.current?.abort();},[active]);useEffect(()=>()=>request.current?.abort(),[]);
 if(denied)return <div className={styles.screen}><h1>Staff access required.</h1><p>Your account cannot currently access moderation tools.</p>{error&&<p role="alert">{error}</p>}<Button disabled={!active||busy} onClick={async()=>{
  if(!active||request.current)return;const controller=new AbortController();request.current=controller;setBusy(true);setError('');
  try{await getReports({status:'open',page:1},controller.signal);if(latest.current&&!controller.signal.aborted)store.allow(accountId);}
  catch{if(!controller.signal.aborted)setError('Staff access could not be verified. Wait if requests are limited, then try again.');}
  finally{request.current=null;setBusy(false);}
 }}>Check staff access again</Button><p><Link href="/account">Back to account</Link></p></div>;
 return <StaffContext.Provider value={{accountId,active}}>{!active&&<p role="status">Checking your session. Staff content is concealed.</p>}<div hidden={!active} inert={!active}>{children}</div></StaffContext.Provider>;
}
