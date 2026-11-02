import { expect, it } from "vitest";
import { collectionFilters, collectionHref } from "./filters";
it('restores scope and page and emits canonical links', () => {
 expect(collectionFilters(new URLSearchParams())).toEqual({scope:'all',page:1,pageSize:20});
 expect(collectionFilters(new URLSearchParams('scope=mine&page=2'))).toEqual({scope:'mine',page:2,pageSize:20});
 expect(collectionHref({scope:'mine',page:2,pageSize:20})).toBe('/collections?scope=mine&page=2');
 expect(collectionHref({scope:'all',page:1,pageSize:20})).toBe('/collections');
});
it.each(['scope=private','scope=mine&scope=all','page=0','page=10001','page=1.5','page=1&page=2','q=test'])('rejects invalid UI filters %s', query => expect(collectionFilters(new URLSearchParams(query))).toBeNull());
