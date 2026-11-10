import 'server-only';
import {isMediaId} from '@/features/catalog/query/filters';
import {staffFilters,staffParams} from '@/features/moderation/query/filters';
import {staffAccountFilters,staffAccountParams} from '@/features/moderation/query/staff-management';
export function moderationRoute(method:string,path:readonly string[]):boolean {
 if(path[0]==='admin')return method==='GET'&&path.length===2&&path[1]==='accounts'||path.length===3&&isMediaId(path[2])&&(path[1]==='moderators'&&['PUT','DELETE'].includes(method)||path[1]==='administrators'&&method==='DELETE');
 if(path[0]!=='moderation')return false;
 if(method==='GET'&&path.length===2)return ['reports','audit'].includes(path[1]);
 if(!['reports','reviews','comments','media','categories','profiles'].includes(path[1])||!isMediaId(path[2]??''))return false;
 return method==='GET'&&path.length===3||method==='POST'&&path.length===4&&path[3]===(path[1]==='reports'?'resolve':'visibility');
}
export function moderationQuery(method:string,path:readonly string[],params:URLSearchParams):URLSearchParams|null {
 if(method==='GET'&&path.length===2&&path[0]==='admin'&&path[1]==='accounts')return staffAccountParams(staffAccountFilters(params,true),true);
 if(method!=='GET'||path.length!==2||path[0]!=='moderation'||!['reports','audit'].includes(path[1]))return null;
 return staffParams(staffFilters(params,path[1] as 'reports'|'audit',true),true);
}
