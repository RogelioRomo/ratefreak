import { isMediaId } from '@/features/catalog/query/filters';
import type { StaffFilters } from '../interfaces/moderation';
export function staffFilters(params:URLSearchParams,kind:'reports'|'audit',api=false):StaffFilters {
 const allowed=kind==='reports'?['status','page']:['targetType','targetId','page'];if(api)allowed.push('pageSize');
 for(const key of params.keys())if(!allowed.includes(key)||params.getAll(key).length!==1)throw Error('Invalid moderation filters.');
 const page=params.get('page')??'1';if(!/^[1-9][0-9]{0,4}$/.test(page)||Number(page)>10000)throw Error('Invalid page.');
 if(params.has('pageSize')&&params.get('pageSize')!=='20')throw Error('Invalid page size.');
 if(kind==='reports'){const status=params.get('status')??'open';if(!['open','resolved'].includes(status))throw Error('Invalid status.');return {page:Number(page),status:status as 'open'|'resolved'};}
 const type=params.get('targetType'),id=params.get('targetId');
 if(type!==null&&type!==''&&!['review','comment','account'].includes(type)||id!==null&&id!==''&&(!isMediaId(id)||id!==id.toLowerCase()))throw Error('Invalid audit target.');
 return {page:Number(page),...(type?{targetType:type as StaffFilters['targetType']}:{}),...(id?{targetId:id}:{})};
}
export function staffParams(filters:StaffFilters,api=false):URLSearchParams {
 const params=new URLSearchParams();if(filters.status)params.set('status',filters.status);if(filters.targetType)params.set('targetType',filters.targetType);if(filters.targetId)params.set('targetId',filters.targetId);params.set('page',String(filters.page));if(api)params.set('pageSize','20');return params;
}
export function safeStaffReturnTo(value:string):string|null {
 const [path,query]=value.split('?');if(value.includes('#')||value.split('?').length>2)return null;
 if(path==='/moderation'||path==='/moderation/audit'){try{staffFilters(new URLSearchParams(query),path.endsWith('audit')?'audit':'reports');return value;}catch{return null;}}
 const parts=path.split('/');return !query&&parts.length===4&&parts[1]==='moderation'&&['reports','reviews','comments'].includes(parts[2])&&isMediaId(parts[3])&&parts[3]===parts[3].toLowerCase()?value:null;
}
