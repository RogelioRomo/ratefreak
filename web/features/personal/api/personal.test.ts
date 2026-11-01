import { beforeEach, expect, it, vi } from "vitest";
import { ownRecord, saveTracking, saveRating, saveReview } from "./personal";
import { resetCsrfState } from "@/shared/api/csrf";
const mock=vi.fn<typeof fetch>();const id='a1111111-1111-1111-1111-111111111111';
beforeEach(()=>{resetCsrfState();mock.mockReset();vi.stubGlobal('fetch',mock);});
it('only404 means absent and propagates cancellation',async()=>{const signal=new AbortController().signal;mock.mockResolvedValueOnce(new Response(null,{status:404})).mockResolvedValueOnce(Response.json({}, {status:503}));expect(await ownRecord(`media/${id}/rating`,signal)).toBeNull();expect(mock.mock.calls[0][1]?.signal).toBe(signal);await expect(ownRecord(`media/${id}/rating`)).rejects.toMatchObject({status:503});});
it('preserves zero, dates and complete review body through CSRF writes',async()=>{
 mock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):Response.json({}));
 await saveTracking(id,{status:'completed',startedAt:'2026-01-01',completedAt:'2026-01-02'});await saveRating(id,0);await saveReview(id,{body:'<b>Literal</b>',isSpoiler:true},'review-id');
 const writes=mock.mock.calls.filter(([url])=>!String(url).endsWith('csrf'));
 expect(writes.map(x=>x[1]?.method)).toEqual(['PUT','PUT','PATCH']);expect(JSON.parse(String(writes[0][1]?.body))).toMatchObject({startedAt:'2026-01-01',completedAt:'2026-01-02'});expect(writes[1][1]?.body).toBe('{"score":0}');expect(writes[2][0]).toBe('/api/v1/reviews/review-id');
});
