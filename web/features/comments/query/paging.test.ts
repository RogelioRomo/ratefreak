import { expect, it } from "vitest";
import { commentPage, discussionHref } from "./paging";
it('defaults and restores page',()=>{expect(commentPage(new URLSearchParams())).toBe(1);expect(commentPage(new URLSearchParams('page=2'))).toBe(2);expect(discussionHref('id',2)).toBe('/reviews/id?page=2');expect(discussionHref('id',1)).toBe('/reviews/id');});
it.each(['page=0','page=10001','page=1&page=2','pageSize=20','author=x','page=1.2'])('rejects %s',q=>expect(commentPage(new URLSearchParams(q))).toBeNull());
