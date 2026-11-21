import {apiRequest} from '@/shared/api/request';
import type {UpdateStatus} from '../interfaces/update-status';
export const getUpdates=(signal?:AbortSignal)=>apiRequest<UpdateStatus>('admin/updates',{signal});
export const checkUpdates=(signal?:AbortSignal)=>apiRequest<UpdateStatus>('admin/updates/check',{method:'POST',body:{},signal});
