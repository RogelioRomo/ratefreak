import {expect,it} from 'vitest';
import {QueryClient} from '@tanstack/react-query';
import {isAllowedRoute} from '@/server/api/route-policy';
import {staffFilters,safeStaffReturnTo} from './filters';
import {refreshModeration} from './keys';
const id='a1111111-1111-1111-1111-111111111111';
it('allows only exact reporting and moderation profile methods',()=>{
 expect(isAllowedRoute('POST',['users',id,'reports'])).toBe(true);
 expect(isAllowedRoute('GET',['moderation','profiles',id])).toBe(true);
 expect(isAllowedRoute('POST',['moderation','profiles',id,'visibility'])).toBe(true);
 for(const [method,path] of [['GET',['users',id,'reports']],['POST',['users','me','reports']],['PUT',['moderation','profiles',id,'visibility']],['POST',['moderation','profiles',id,'extra']]] as const)expect(isAllowedRoute(method,path)).toBe(false);
 expect(safeStaffReturnTo(`/moderation/profiles/${id}`)).toBe(`/moderation/profiles/${id}`);
 expect(staffFilters(new URLSearchParams('targetType=profile'),'audit').targetType).toBe('profile');
});
it('invalidates all public identity surfaces and the owners session after profile decisions',()=>{
 const client=new QueryClient();const roots=['public-profile','connections','community','review-discussion','comments','collections','session'];
 for(const root of roots)client.setQueryData([root],{displayName:'Old'});
 refreshModeration(client,'staff');
 for(const root of roots)expect(client.getQueryState([root])?.isInvalidated,root).toBe(true);
});
