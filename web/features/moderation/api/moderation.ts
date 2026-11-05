import {apiRequest} from '@/shared/api/request';
import {staffParams} from '../query/filters';
import type {AuditEntry,ModerationTarget,ReportDetail,ReportSummary,ResolveInput,StaffFilters,StaffPage,TargetSegment,VisibilityInput} from '../interfaces/moderation';
export const getReports=(filters:StaffFilters,signal?:AbortSignal)=>apiRequest<StaffPage<ReportSummary>>(`moderation/reports?${staffParams(filters,true)}`,{signal});
export const getReport=(id:string,signal?:AbortSignal)=>apiRequest<ReportDetail>(`moderation/reports/${id}`,{signal});
export const getTarget=(segment:TargetSegment,id:string,signal?:AbortSignal)=>apiRequest<ModerationTarget>(`moderation/${segment}/${id}`,{signal});
export const getAudit=(filters:StaffFilters,signal?:AbortSignal)=>apiRequest<StaffPage<AuditEntry>>(`moderation/audit?${staffParams(filters,true)}`,{signal});
export const resolveReport=(id:string,body:ResolveInput,signal?:AbortSignal)=>apiRequest<void>(`moderation/reports/${id}/resolve`,{method:'POST',body,signal});
export const setVisibility=(segment:TargetSegment,id:string,body:VisibilityInput,signal?:AbortSignal)=>apiRequest<void>(`moderation/${segment}/${id}/visibility`,{method:'POST',body,signal});
