import {apiRequest} from '@/shared/api/request';
import {staffAccountParams} from '../query/staff-management';
import type {StaffPage} from '../interfaces/moderation';
import type {StaffAccount,StaffAccountFilters} from '../interfaces/staff-management';
export const getStaffAccounts=(filters:StaffAccountFilters,signal?:AbortSignal)=>apiRequest<StaffPage<StaffAccount>>(`admin/accounts?${staffAccountParams(filters,true)}`,{signal});
export const setModerator=(id:string,grant:boolean,reason:string,signal?:AbortSignal)=>apiRequest<void>(`admin/moderators/${id}`,{method:grant?'PUT':'DELETE',body:{reason},signal});
