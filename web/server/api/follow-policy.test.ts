import { expect, it } from "vitest";
import { isAllowedRoute } from "./route-policy";
import { followList, followQuery } from "./follow-policy";
const id="a1111111-1111-1111-1111-111111111111";
it("allows exact follow methods and public lists",()=>{
 for(const direction of ['followers','following'])expect(isAllowedRoute('GET',['users',id,direction])).toBe(true);
 for(const method of ['PUT','DELETE'])expect(isAllowedRoute(method,['users',id,'follow'])).toBe(true);
 for(const path of [['users','me','follow'],['users',id,'follow','extra'],['users','invalid','followers']])expect(isAllowedRoute('GET',path)).toBe(false);
 expect(isAllowedRoute('GET',['users',id,'follow'])).toBe(false);expect(isAllowedRoute('POST',['users',id,'follow'])).toBe(false);
 expect(followList(['users',id,'followers'])).toBe(true);
});
it('validates and canonicalizes paging',()=>expect(followQuery(new URLSearchParams('page=02&pageSize=20')).toString()).toBe('page=2&pageSize=20'));
it.each(['page=0','page=10001','page=1&page=2','pageSize=101','pageSize=0','userId=x','page=1.2'])('rejects %s',q=>expect(()=>followQuery(new URLSearchParams(q))).toThrow());
