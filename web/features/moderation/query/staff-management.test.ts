import {expect,it} from 'vitest';
import {isAllowedRoute} from '@/server/api/route-policy';
import {moderationQuery} from '@/server/api/moderation-policy';
import {safeStaffReturnTo} from './filters';
import {staffAccountFilters,staffAccountParams} from './staff-management';
const id='a1111111-1111-1111-1111-111111111111';
it('allows only the directory read and exact moderator writes',()=>{
 expect(isAllowedRoute('GET',['admin','accounts'])).toBe(true);
 for(const method of ['PUT','DELETE'])expect(isAllowedRoute(method,['admin','moderators',id])).toBe(true);
 for(const [method,path] of [['POST',['admin','moderators',id]],['GET',['admin','moderators',id]],['PUT',['admin','accounts',id]],['PUT',['admin','moderators','bad']],['PUT',['admin','moderators',id,'extra']]] as const)expect(isAllowedRoute(method,path)).toBe(false);
});
it('canonicalizes bounded account filters and preserves literal search text',()=>{
 expect(staffAccountFilters(new URLSearchParams())).toEqual({q:'',staffOnly:true,page:1});
 const f=staffAccountFilters(new URLSearchParams('q=%20A_100%25%20&staffOnly=false&page=2'));
 expect(f).toEqual({q:'A_100%',staffOnly:false,page:2});
 expect(moderationQuery('GET',['admin','accounts'],staffAccountParams(f,true))?.toString()).toBe('q=A_100%25&staffOnly=false&page=2&pageSize=20');
 for(const query of ['q=a&q=b','staffOnly=yes','page=0','page=01','page=10001','x=y','pageSize=100',`q=${'a'.repeat(101)}`])expect(()=>moderationQuery('GET',['admin','accounts'],new URLSearchParams(query))).toThrow();
 expect(safeStaffReturnTo('/moderation/staff?staffOnly=false&q=Reader')).toBe('/moderation/staff?staffOnly=false&q=Reader');
 expect(safeStaffReturnTo('/moderation/staff?unknown=1')).toBeNull();
});
