import { beforeEach, expect, it, vi } from "vitest";
import { getProviders, getExternalSearch, getExternalMedia, importMedia } from "./providers";
import { resetCsrfState } from "@/shared/api/csrf";
const fetchMock=vi.fn<typeof fetch>();
beforeEach(()=>{vi.stubGlobal('fetch',fetchMock);fetchMock.mockReset();resetCsrfState();});
it('uses real client and propagates search/preview cancellation',async()=>{
 fetchMock.mockImplementation(async()=>Response.json({}));const signal=new AbortController().signal;
 await getProviders(signal);await getExternalSearch({q:'A & B',provider:'tmdb',type:'movie',page:2},signal);await getExternalMedia({provider:'igdb',type:'video_game',externalId:'1942'},signal);
 expect(fetchMock.mock.calls.map(x=>x[0])).toEqual(['/api/v1/media/providers','/api/v1/media/external/search?q=A+%26+B&provider=tmdb&type=movie&page=2','/api/v1/media/external/igdb/video_game/1942']);
 for(const call of fetchMock.mock.calls)expect(call[1]?.signal).toBe(signal);
});
it('imports only identity via CSRF client without retry',async()=>{
 fetchMock.mockResolvedValueOnce(Response.json({requestToken:'token'})).mockResolvedValueOnce(Response.json({mediaId:'id',created:true}));
 await importMedia({provider:'tmdb',externalId:'11',mediaType:'movie'});
 expect(fetchMock.mock.calls[1][0]).toBe('/api/v1/media/import');
 expect(fetchMock.mock.calls[1][1]).toMatchObject({method:'POST',body:JSON.stringify({provider:'tmdb',externalId:'11',mediaType:'movie'}),headers:expect.objectContaining({'X-CSRF-TOKEN':'token'})});
});
