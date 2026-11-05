import {beforeEach,expect,it,vi} from 'vitest';
import {resolveReport,setVisibility} from './moderation';
import {resetCsrfState} from '@/shared/api/csrf';
const mock=vi.fn<typeof fetch>();
beforeEach(()=>{resetCsrfState();vi.stubGlobal('fetch',mock);mock.mockReset();mock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):new Response(null,{status:204}));});
it('preserves microsecond version strings and explicit null for deleted targets',async()=>{
 const version='2026-11-05T14:42:19.123456+00:00';
 await resolveReport('report',{decision:'hide',reason:'Reason',expectedTargetUpdatedAt:version});
 await setVisibility('comments','target',{hidden:false,reason:'Restore',expectedTargetUpdatedAt:version});
 await resolveReport('deleted',{decision:'dismiss',reason:'Deleted',expectedTargetUpdatedAt:null});
 const writes=mock.mock.calls.filter(([,o])=>o?.method==='POST');
 expect(writes.map(([url])=>url)).toEqual(['/api/v1/moderation/reports/report/resolve','/api/v1/moderation/comments/target/visibility','/api/v1/moderation/reports/deleted/resolve']);
 expect(writes.map(([,o])=>JSON.parse(String(o?.body)).expectedTargetUpdatedAt)).toEqual([version,version,null]);
});
