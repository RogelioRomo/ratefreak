'use client';
import Link from 'next/link';
import {useUpdates} from '../hooks/use-updates';
export function UpdateNotice(){
  const {staff,denied,query}=useUpdates();
  if(!staff.active||denied||query.isError||!query.data)return null;
  return <span><Link href="/moderation/updates">Updates</Link>{query.data.state==='available'&&<span role="status"> · New release available</span>}</span>;
}
