import { expect,it } from 'vitest';
import { isAllowedRoute } from './route-policy';
import { moderationQuery } from './moderation-policy';
const id='a1111111-1111-1111-1111-111111111111';
it('allows exact staff methods without enabling admin routes',()=>{
 for(const path of [['moderation','reports'],['moderation','audit'],...['reports','reviews','comments'].map(s=>['moderation',s,id])])expect(isAllowedRoute('GET',path)).toBe(true);
 for(const [segment,action] of [['reports','resolve'],['reviews','visibility'],['comments','visibility']]){expect(isAllowedRoute('POST',['moderation',segment,id,action])).toBe(true);expect(isAllowedRoute('GET',['moderation',segment,id,action])).toBe(false);}
 for(const path of [['admin','moderators',id],['moderation','reports','bad'],['moderation','reports',id,'extra']])expect(isAllowedRoute('POST',path)).toBe(false);
});
it('validates list queries and rejects unknown/duplicate filters',()=>{
 expect(moderationQuery('GET',['moderation','reports'],new URLSearchParams('status=resolved&page=2&pageSize=20'))?.toString()).toBe('status=resolved&page=2&pageSize=20');
 for(const query of ['status=bad','page=0','page=01','page=10001','page=2&page=3','x=y','pageSize=100'])expect(()=>moderationQuery('GET',['moderation','reports'],new URLSearchParams(query))).toThrow();
 expect(()=>moderationQuery('GET',['moderation','audit'],new URLSearchParams('targetType=bad'))).toThrow();
 expect(()=>moderationQuery('GET',['moderation','audit'],new URLSearchParams('targetId=bad'))).toThrow();
 expect(moderationQuery('GET',['moderation','reports',id],new URLSearchParams())).toBeNull();
});
