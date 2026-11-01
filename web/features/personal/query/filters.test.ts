import { expect, it } from "vitest";
import { libraryFilters, libraryHref, reviewPage } from "./filters";
it('defaults and serializes fixed page size',()=>{expect(libraryFilters(new URLSearchParams())).toEqual({status:'',type:'',page:1,pageSize:20});expect(libraryHref({status:'completed',type:'album',page:2,pageSize:20})).toBe('/library?status=completed&type=album&page=2');});
it.each(['status=x','page=0','page=1&page=2','type=constructor','q=x','pageSize=50'])('rejects %s',q=>expect(libraryFilters(new URLSearchParams(q))).toBeNull());
it('validates independent review pagination',()=>{expect(reviewPage(new URLSearchParams('reviewsPage=2'))).toBe(2);expect(reviewPage(new URLSearchParams('reviewsPage=2&reviewsPage=3'))).toBeNull();});
