'use client';
import {useEffect,useLayoutEffect,useRef,useState} from 'react';
import Link from 'next/link';
import {useStaff} from '@/features/moderation/query/context';
import {staffRequest} from '@/features/moderation/query/access';
import {StaffNav} from '@/features/moderation/components/staff-nav';
import {Button} from '@/components/ui/button';
import {LoadingState} from '@/components/ui/loading-state';
import {useUpdates} from '../hooks/use-updates';
import {checkUpdates} from '../api/updates';
import type {UpdateStatus} from '../interfaces/update-status';
import styles from '@/features/moderation/components/moderation.module.css';
const messages:Record<UpdateStatus['state'],string>={
  disabled:'Update checks are disabled. Your operator can enable them in the instance configuration.',
  unchecked:'Updates have not been checked yet.',checking:'Checking for updates…',
  current:'This instance is up to date.',available:'A newer stable release is available.',
  unavailable:'Update availability could not be checked.','no-release':'No stable release has been published yet.',
  development:'This is a development or custom build.',
};
function timestamp(value:string|null){return value?new Date(value).toLocaleString():'Never';}
export function UpdatesScreen(){const staff=useStaff();return <UpdatesContent key={staff.accountId}/>;}
function UpdatesContent(){
  const {staff,client,store,denied,query}=useUpdates();
  const [busy,setBusy]=useState(false),[feedback,setFeedback]=useState('');
  const request=useRef<AbortController|null>(null),active=useRef(staff.active);
  useLayoutEffect(()=>{active.current=staff.active;if(!staff.active)request.current?.abort();},[staff.active]);
  useEffect(()=>()=>request.current?.abort(),[]);
  async function check(){
    if(request.current||!staff.active||!query.data?.canCheck)return;
    const controller=new AbortController();request.current=controller;setBusy(true);setFeedback('');
    try{
      const result=await staffRequest(client,staff.accountId,checkUpdates,controller.signal,'admin');
      if(!active.current||controller.signal.aborted)return;
      client.setQueryData(['staff',staff.accountId,'admin','updates'],result);
    }catch{if(active.current&&!controller.signal.aborted)setFeedback('The check could not be completed. Try again when checks are available.');}
    finally{request.current=null;setBusy(false);}
  }
  const data=staff.active&&!denied&&!query.isError?query.data:undefined;
  return <div className={styles.screen}><StaffNav/><h1>Application updates.</h1>
    {!staff.active?<LoadingState label="Checking your session…"/>:denied?<section><h2>Administrator access required.</h2><p>Update checks are available to current administrators.</p><Button onClick={()=>store.allow(staff.accountId,'admin')}>Check administrator access again</Button></section>:query.isError?<section role="alert"><p>Update information could not be loaded.</p><Button onClick={()=>void query.refetch()}>Reload update information</Button></section>:!data?<LoadingState label="Loading update information…"/>:<section aria-label="Release information" aria-busy={busy||query.isFetching}>
      <p role="status">{busy?'Checking for updates…':messages[data.state]}</p>
      <dl><dt>Installed version</dt><dd>{data.installed.version}</dd><dt>Source revision</dt><dd className={styles.meta}>{data.installed.revision}</dd>
        {data.latestVersion&&<><dt>Latest stable version</dt><dd>{data.latestVersion}</dd></>}
        <dt>Last successful check</dt><dd>{timestamp(data.lastSuccessAt)}</dd><dt>Last attempt</dt><dd>{timestamp(data.lastAttemptAt)}</dd>
      </dl>
      {data.stale&&<p>Last known release information may be outdated.</p>}
      <Button disabled={busy||!data.canCheck} onClick={()=>void check()}>Check for updates</Button>
      {data.enabled&&!data.canCheck&&data.nextCheckAt&&<p className={styles.meta}>Next manual check available: {timestamp(data.nextCheckAt)}</p>}
      {feedback&&<p role="alert">{feedback}</p>}
      <p>Your operator installs updates using the documented backup and migration procedure.</p>
      {data.releaseUrl&&<p><a href={data.releaseUrl}>Release notes</a></p>}
      <p><a href={`https://github.com/RogelioRomo/ratefreak/blob/${data.latestVersion?`v${encodeURIComponent(data.latestVersion)}`:'main'}/docs/self-hosting.md#upgrade`}>Upgrade instructions</a> · <a href="https://github.com/RogelioRomo/ratefreak/releases">All releases</a></p>
      {!data.enabled&&<p className={styles.meta}>When enabled, this server contacts GitHub and shares its network address and RateFreak version. Account and instance data are not sent.</p>}
    </section>}
    <p><Link href="/account">Back to account</Link></p>
  </div>;
}
