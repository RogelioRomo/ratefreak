import type { ReactNode } from 'react';
export type TargetSegment = 'reviews' | 'comments' | 'media' | 'categories';
export interface StaffIdentity { id:string; handle:string; displayName:string }
export interface ModerationTarget { id:string; targetType:'review'|'comment'|'item'|'category'; author:StaffIdentity|null; body:string; isSpoiler:boolean; hidden:boolean; updatedAt:string; description?:string|null; imageReference?:string|null }
export interface ReportSummary { id:string; targetType:'review'|'comment'|'item'|'category'; targetId:string; reporter:StaffIdentity|null; reason:string; status:'open'|'resolved'; createdAt:string }
export interface ReportDetail { report:ReportSummary; details:string|null; target:ModerationTarget|null; decision:string|null; resolutionReason:string|null; resolvedAt:string|null }
export interface AuditEntry { id:string; createdAt:string; actorId:string|null; actorKind:string; action:string; targetType:string; targetId:string; reportId:string|null; reason:string; before:boolean|null; after:boolean|null; reviewedUpdatedAt:string|null }
export interface StaffPage<T> { items:T[]; page:number; pageSize:number; totalCount:number }
export interface ResolveInput { decision:'hide'|'dismiss'; reason:string; expectedTargetUpdatedAt:string|null }
export interface VisibilityInput { hidden:boolean; reason:string; expectedTargetUpdatedAt:string }
export interface StaffFilters { page:number; status?:'open'|'resolved'; targetType?:'review'|'comment'|'item'|'category'|'account'; targetId?:string }
export interface StaffContextValue { accountId:string; active:boolean }
export interface StaffBoundaryProps { children:ReactNode }
export interface StaffDetailProps { id:string }
export interface StaffTargetProps extends StaffDetailProps { segment:TargetSegment }
export interface StaffPageProps { params:Promise<{id:string}> }
export interface StaffEditorProps { target:ModerationTarget|null; report?:ReportSummary; available:boolean; segment?:TargetSegment; onSaved:()=>void; onReload:()=>Promise<unknown> }
export interface StaffPagingProps { page:number; total:number; href:(page:number)=>string }
