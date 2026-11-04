import { expect, it } from "vitest";
import { validatePersonalQuery } from "./personal-policy";
import { isAllowedRoute } from "./route-policy";
const id='a1111111-1111-1111-1111-111111111111';
it('accepts exact feature methods only',()=>{
 for(const method of ['GET','PUT','DELETE'])expect(isAllowedRoute(method,['users','me','library',id])).toBe(true);
 for(const field of ['rating','review','ratings','reviews'])expect(isAllowedRoute('GET',['media',id,field])).toBe(true);
 expect(isAllowedRoute('PUT',['media',id,'rating'])).toBe(true);expect(isAllowedRoute('POST',['media',id,'reviews'])).toBe(true);
 expect(isAllowedRoute('PATCH',['reviews',id])).toBe(true);expect(isAllowedRoute('DELETE',['reviews',id])).toBe(true);
 expect(isAllowedRoute('GET',['reviews',id])).toBe(true);expect(isAllowedRoute('PATCH',['users','me'])).toBe(true);
});
it('normalizes allowed list filters',()=>expect(validatePersonalQuery(new URLSearchParams('status=completed&type=album&page=2&pageSize=20'),'library').toString()).toBe('status=completed&type=album&page=2&pageSize=20'));
it.each(['status=unknown','type=book','page=0','page=10001','pageSize=101','page=1&page=2','userId=spoof'])('rejects %s',q=>expect(()=>validatePersonalQuery(new URLSearchParams(q),'library')).toThrow());
it('reviews accept only paging',()=>expect(()=>validatePersonalQuery(new URLSearchParams('status=planned'),'reviews')).toThrow());
