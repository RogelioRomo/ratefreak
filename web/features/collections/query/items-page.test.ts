import { expect,it } from 'vitest';
import { itemsPage,itemsHref } from './items-page';
it('validates independent entry paging and creates canonical parent links',()=>{expect(itemsPage(new URLSearchParams())).toBe(1);expect(itemsPage(new URLSearchParams('itemsPage=2'))).toBe(2);for(const q of ['itemsPage=0','itemsPage=10001','itemsPage=2&itemsPage=3','page=2'])expect(itemsPage(new URLSearchParams(q))).toBeNull();expect(itemsHref('ID',2)).toBe('/collections/id?itemsPage=2');});
