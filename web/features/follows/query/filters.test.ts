import { expect, it } from "vitest";
import { connectionFilters, connectionHref } from "./filters";
it('defaults to followers and resets direction paging in links',()=>{expect(connectionFilters(new URLSearchParams())).toEqual({direction:'followers',page:1});expect(connectionHref('Owner',{direction:'following',page:1})).toBe('/users/Owner?connections=following');});
it.each(['connections=x','connections=followers&connections=following','page=0','page=10001','page=2&page=3','pageSize=20','target=x'])('rejects invalid URL %s',q=>expect(connectionFilters(new URLSearchParams(q))).toBeNull());
it('restores a submitted page',()=>expect(connectionFilters(new URLSearchParams('connections=following&page=2'))).toEqual({direction:'following',page:2}));
