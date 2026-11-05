'use client';
import {useQuery,useQueryClient} from '@tanstack/react-query';
import {useStaff} from '../query/context';
import {staffRequest} from '../query/access';
export function useStaffQuery<T>(key:readonly unknown[],operation:(signal:AbortSignal)=>Promise<T>,enabled=true){
 const client=useQueryClient(),staff=useStaff();
 return useQuery({queryKey:['staff',staff.accountId,...key],queryFn:({signal})=>staffRequest(client,staff.accountId,operation,signal),enabled:staff.active&&enabled,retry:false,staleTime:0,gcTime:0});
}
