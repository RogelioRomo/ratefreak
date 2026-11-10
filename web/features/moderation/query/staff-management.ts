import type {StaffAccountFilters} from '../interfaces/staff-management';
export function staffAccountFilters(params:URLSearchParams,api=false):StaffAccountFilters {
 const allowed=['q','staffOnly','page',...(api?['pageSize']:[])];
 for(const key of params.keys())if(!allowed.includes(key)||params.getAll(key).length!==1)throw Error('Invalid account filters.');
 const q=params.get('q')??'',staffOnly=params.get('staffOnly')??'true',page=params.get('page')??'1';
 if(q.length>100||/[\u0000-\u001f\u007f]/.test(q)||!['true','false'].includes(staffOnly)||!/^[1-9][0-9]{0,4}$/.test(page)||Number(page)>10000||params.has('pageSize')&&params.get('pageSize')!=='20')throw Error('Invalid account filters.');
 return {q:q.trim(),staffOnly:staffOnly==='true',page:Number(page)};
}
export function staffAccountParams(filters:StaffAccountFilters,api=false):URLSearchParams {
 const params=new URLSearchParams();if(filters.q)params.set('q',filters.q);params.set('staffOnly',String(filters.staffOnly));params.set('page',String(filters.page));if(api)params.set('pageSize','20');return params;
}
