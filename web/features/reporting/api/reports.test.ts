import { beforeEach, expect, it, vi } from "vitest";
import { createReport } from "./reports";
import { resetCsrfState } from "@/shared/api/csrf";
const fetchMock = vi.fn<typeof fetch>();
const id = 'a1111111-1111-1111-1111-111111111111';
beforeEach(() => { resetCsrfState(); fetchMock.mockReset(); vi.stubGlobal('fetch', fetchMock); });
it.each(['review', 'comment'] as const)('posts an explicit %s report with CSRF and cancellation', async type => {
 const acknowledgement = { id, status: 'open', createdAt: '2026-11-05T00:00:00Z' };
 fetchMock.mockImplementation(async url => String(url).endsWith('csrf') ? Response.json({ requestToken: 'token' }) : Response.json(acknowledgement, {status:201}));
 const signal = new AbortController().signal;
 expect(await createReport({type,id,authorId:'author'}, {reason:'other',details:'Private details'}, signal)).toEqual(acknowledgement);
 const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/reports'))!;
 expect(call[0]).toBe(`/api/v1/${type === 'review' ? 'reviews' : 'comments'}/${id}/reports`);
 expect(call[1]).toMatchObject({method:'POST',signal,body:'{"reason":"other","details":"Private details"}'});
});
it('propagates duplicate acknowledgement without replaying the POST',async()=>{
 fetchMock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):new Response(null,{status:409}));
 await expect(createReport({type:'review',id,authorId:'author'},{reason:'spam'})).rejects.toMatchObject({status:409});
 expect(fetchMock.mock.calls.filter(([url])=>String(url).endsWith('/reports'))).toHaveLength(1);
});
