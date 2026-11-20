import { expect, it } from "vitest";
import { libraryFilters, libraryHref, reviewPage } from "./filters";
it('defaults and serializes fixed page size',()=>{expect(libraryFilters(new URLSearchParams())).toEqual({status:'',type:'',page:1,pageSize:20});expect(libraryHref({status:'completed',type:'album',page:2,pageSize:20})).toBe('/library?status=completed&type=album&page=2');});
it.each(['status=x','page=0','page=1&page=2','type=constructor','q='+ 'x'.repeat(201),'sort=unknown','q=a&q=b','pageSize=50'])('rejects %s',q=>expect(libraryFilters(new URLSearchParams(q))).toBeNull());
it('validates independent review pagination',()=>{expect(reviewPage(new URLSearchParams('reviewsPage=2'))).toBe(2);expect(reviewPage(new URLSearchParams('reviewsPage=2&reviewsPage=3'))).toBeNull();});

it('round trips trimmed search and ordering with fixed paging',()=>{
 const filters=libraryFilters(new URLSearchParams('q=%20film%20&sort=highest_rated&status=planned&page=2'))!;
 expect(filters.q).toBe('film');expect(filters.sort).toBe('highest_rated');
 expect(libraryHref(filters)).toBe('/library?status=planned&q=film&sort=highest_rated&page=2');
 expect(libraryFilters(new URLSearchParams('q=%20%20'))?.q).toBeUndefined();
});
