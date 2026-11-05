import {expect,it} from 'vitest';
import {safeStaffReturnTo,staffFilters} from './filters';
const id='a1111111-1111-1111-1111-111111111111';
it('accepts exact internal staff continuations and optional empty audit form controls',()=>{for(const value of ['/moderation','/moderation?status=resolved&page=2',`/moderation/reports/${id}`,`/moderation/comments/${id}`,`/moderation/reviews/${id}`,'/moderation/audit?targetType=&targetId='])expect(safeStaffReturnTo(value)).toBe(value);expect(staffFilters(new URLSearchParams('targetType=&targetId='),'audit')).toEqual({page:1});});
it.each(['//moderation','/moderation-other','/moderation?status=unknown','/moderation?page=2&page=3','/moderation#x',`/moderation/reports/${id}?next=evil`,'/moderation/audit?targetId=bad','/moderation/audit?targetType=&targetType='])('rejects ambiguous continuation %s',value=>expect(safeStaffReturnTo(value)).toBeNull());
