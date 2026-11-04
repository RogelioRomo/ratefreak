import { expect, it } from "vitest";
import { isAllowedRoute } from "./route-policy";
import { commentList } from "./comment-policy";
const id='a1111111-1111-1111-1111-111111111111';
it('allows exact public discussion and author mutation methods',()=>{
 expect(isAllowedRoute('GET',['reviews',id])).toBe(true);
 for(const method of ['GET','POST'])expect(isAllowedRoute(method,['reviews',id,'comments'])).toBe(true);
 for(const method of ['GET','PATCH','DELETE'])expect(isAllowedRoute(method,['comments',id])).toBe(true);
 for(const path of [['comments','me'],['reviews',id,'comments','extra'],['comments',id,'extra']])expect(isAllowedRoute('GET',path)).toBe(false);
 expect(isAllowedRoute('PUT',['comments',id])).toBe(false);expect(isAllowedRoute('DELETE',['reviews',id,'comments'])).toBe(false);
 expect(commentList(['reviews',id,'comments'])).toBe(true);
});
