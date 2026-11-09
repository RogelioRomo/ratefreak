import { beforeEach, expect, it, vi } from "vitest";
import { validateCollectionQuery } from "./collection-policy";
import { isAllowedRoute } from "./route-policy";
import { proxyRequest } from "./proxy";
const id='a1111111-1111-1111-1111-111111111111';
const fetchMock=vi.fn<typeof fetch>();
beforeEach(()=>{vi.stubEnv('RATEFREAK_API_URL','http://api.test');vi.stubEnv('NODE_ENV','development');vi.stubGlobal('fetch',fetchMock);fetchMock.mockReset();fetchMock.mockResolvedValue(Response.json({}));});
it('opens only exact collection metadata routes',()=>{
 for(const method of ['GET','POST'])expect(isAllowedRoute(method,['collections'])).toBe(true);
 for(const method of ['GET','PATCH'])expect(isAllowedRoute(method,['collections',id])).toBe(true);
 for(const path of [['collections','bad'],['collections',id,'unknown'],['collections',id,'fields']])expect(isAllowedRoute('GET',path)).toBe(false);
 for(const method of ['PUT','DELETE'])expect(isAllowedRoute(method,['collections',id])).toBe(false);
});
it.each(['ownOnly=1','ownOnly=True','ownOnly=true&ownOnly=false','page=0','page=10001','pageSize=101','owner=spoof'])('rejects query %s',q=>expect(()=>validateCollectionQuery(new URLSearchParams(q))).toThrow());
it('validates and forwards collection list filters without caching',async()=>{
 expect(validateCollectionQuery(new URLSearchParams('ownOnly=false&page=10000&pageSize=100')).toString()).toBe('ownOnly=false&page=10000&pageSize=100');
 const r=await proxyRequest(new Request('http://web.test/api/v1/collections?ownOnly=true&page=2&pageSize=20'),['collections']);
 expect(r.status).toBe(200);expect(r.headers.get('cache-control')).toBe('no-store');expect(fetchMock.mock.calls[0][0]).toBe('http://api.test/api/v1/collections?ownOnly=true&page=2&pageSize=20');
});
it('rejects detail query and foreign-origin mutation locally',async()=>{
 expect((await proxyRequest(new Request(`http://web.test/api/v1/collections/${id}?page=2`),['collections',id])).status).toBe(404);
 vi.stubEnv('RATEFREAK_WEB_ORIGIN','http://web.test');vi.stubEnv('RATEFREAK_API_URL','http://localhost:5080');vi.stubEnv('RATEFREAK_PROXY_SECRET','b'.repeat(64));vi.stubEnv('RATEFREAK_CLIENT_IP_MODE','loopback');
 expect((await proxyRequest(new Request(`http://web.test/api/v1/collections/${id}`,{method:'PATCH',headers:{Origin:'http://evil.test','Content-Type':'application/json'},body:'{}'}),['collections',id])).status).toBe(403);
 expect(fetchMock).not.toHaveBeenCalled();
});

it.each(['POST','PATCH'])('retains mutation protections for %s',async method=>{
 vi.stubEnv('RATEFREAK_WEB_ORIGIN','http://web.test');vi.stubEnv('RATEFREAK_API_URL','http://localhost:5080');vi.stubEnv('RATEFREAK_PROXY_SECRET','b'.repeat(64));vi.stubEnv('RATEFREAK_CLIENT_IP_MODE','loopback');
 const path=method==='POST'?['collections']:['collections',id];
 const send=(headers:Record<string,string>,body='{}')=>proxyRequest(new Request(`http://web.test/api/v1/${path.join('/')}`,{method,headers,body}),path);
 const headers={Origin:'http://web.test','Content-Type':'application/json','X-CSRF-TOKEN':'token'};
 expect((await send({...headers,'Content-Type':'text/plain'})).status).toBe(415);
 expect((await send({...headers,'X-CSRF-TOKEN':''})).status).toBe(400);
 expect((await send(headers,'x'.repeat(70000))).status).toBe(413);
 expect(fetchMock).not.toHaveBeenCalled();
 const response=await send(headers);expect(response.status).toBe(200);expect(response.headers.get('cache-control')).toBe('no-store');
 const forwarded=new Headers(fetchMock.mock.calls[0][1]?.headers);expect(forwarded.get('X-CSRF-TOKEN')).toBe('token');expect(forwarded.get('X-RateFreak-Proxy-Key')).toBe('b'.repeat(64));
});
it('retires all field and impact routes while keeping list membership methods', async () => {
 for (const method of ['POST','PATCH','DELETE','GET']) {
   expect(isAllowedRoute(method,['collections',id,'fields'])).toBe(false);
   expect(isAllowedRoute(method,['collections',id,'fields',id])).toBe(false);
   expect(isAllowedRoute(method,['collections',id,'fields',id,'deletion-impact'])).toBe(false);
 }
 for (const method of ['PATCH','DELETE','GET']) expect(isAllowedRoute(method,['collections',id,'items',id])).toBe(true);
 expect(isAllowedRoute('POST',['collections',id,'items'])).toBe(true);
});
it.each([['POST','items'],['PATCH','items'],['DELETE','items']])('new %s %s path retains exact Origin enforcement',async(method,kind)=>{
 vi.stubEnv('RATEFREAK_WEB_ORIGIN','http://web.test');vi.stubEnv('RATEFREAK_API_URL','http://localhost:5080');vi.stubEnv('RATEFREAK_PROXY_SECRET','b'.repeat(64));vi.stubEnv('RATEFREAK_CLIENT_IP_MODE','loopback');const path=method==='POST'?['collections',id,kind]:['collections',id,kind,id];const query=method==='DELETE'?'?expectedRevision=1':'';
 expect((await proxyRequest(new Request(`http://web.test/api/v1/${path.join('/')}${query}`,{method,headers:{Origin:'http://evil.test','Content-Type':'application/json','X-CSRF-TOKEN':'token'},body:'{}'}),path)).status).toBe(403);expect(fetchMock).not.toHaveBeenCalled();
});

it('only allows revision-checked unavailable membership removal',async()=>{
 const path=['collections',id,'unavailable-items'];
 expect(isAllowedRoute('DELETE',path)).toBe(true);
 for(const method of ['GET','POST','PATCH'])expect(isAllowedRoute(method,path)).toBe(false);
 expect((await proxyRequest(new Request(`http://web.test/api/v1/${path.join('/')}`,{method:'DELETE'}),path)).status).toBe(400);
});
