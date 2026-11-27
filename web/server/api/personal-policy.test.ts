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
it.each(['status=unknown','type=podcast','page=0','page=10001','pageSize=101','page=1&page=2','userId=spoof'])('rejects %s',q=>expect(()=>validatePersonalQuery(new URLSearchParams(q),'library')).toThrow());
it('accepts the new book library filter',()=>expect(validatePersonalQuery(new URLSearchParams('type=book'),'library').get('type')).toBe('book'));
it('reviews accept only paging',()=>expect(()=>validatePersonalQuery(new URLSearchParams('status=planned'),'reviews')).toThrow());

it('accepts library search, custom types, ordering and status PATCH only',()=>{
 expect(validatePersonalQuery(new URLSearchParams('q=%20film%20&sort=lowest_rated&type=custom'),'library').toString()).toBe('type=custom&q=film&sort=lowest_rated');
 expect(isAllowedRoute('PATCH',['users','me','library',id,'status'])).toBe(true);
 expect(isAllowedRoute('PUT',['users','me','library',id,'status'])).toBe(false);
});
it.each(['q='+ 'a'.repeat(201),'q=a&q=b','sort=x','sort=title&sort=title'])('rejects new invalid query %s',q=>expect(()=>validatePersonalQuery(new URLSearchParams(q),'library')).toThrow());

it('rejects embedded NUL in library search',()=>{expect(()=>validatePersonalQuery(new URLSearchParams("q=a%00b"),"library")).toThrow();});
it('allows only the owner recent ratings read route',()=>{
 expect(isAllowedRoute('GET',['users','me','ratings','recent'])).toBe(true);
 expect(isAllowedRoute('POST',['users','me','ratings','recent'])).toBe(false);
 expect(isAllowedRoute('GET',['users','someone','ratings','recent'])).toBe(false);
});
