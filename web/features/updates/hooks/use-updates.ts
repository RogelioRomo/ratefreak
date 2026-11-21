'use client';
import {useSyncExternalStore} from 'react';
import {useQueryClient} from '@tanstack/react-query';
import {useStaff} from '@/features/moderation/query/context';
import {accessStore} from '@/features/moderation/query/access';
import {useStaffQuery} from '@/features/moderation/hooks/use-staff-query';
import {getUpdates} from '../api/updates';
export function useUpdates(){
  const staff=useStaff(),client=useQueryClient(),store=accessStore(client);
  const denied=useSyncExternalStore(store.subscribe,()=>store.denied(staff.accountId,'admin'),()=>0);
  const query=useStaffQuery(['updates'],getUpdates,!denied,'admin');
  return {staff,client,store,denied,query};
}
