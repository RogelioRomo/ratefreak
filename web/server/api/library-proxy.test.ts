import {beforeEach,expect,it,vi} from "vitest";
import {proxyRequest} from "./proxy";
const id="a1111111-1111-1111-1111-111111111111";const upstream=vi.fn<typeof fetch>();
beforeEach(()=>{vi.stubEnv("NODE_ENV","development");vi.stubEnv("RATEFREAK_API_URL","http://localhost:5080");vi.stubEnv("RATEFREAK_WEB_ORIGIN","http://web.test");vi.stubEnv("RATEFREAK_PROXY_SECRET","b".repeat(64));vi.stubEnv("RATEFREAK_CLIENT_IP_MODE","loopback");upstream.mockReset();upstream.mockImplementation(async()=>Response.json({}));vi.stubGlobal("fetch",upstream);});
it('forwards account binding only for the dedicated status operation',async()=>{
 for(const status of [true,false]){
 const path=['users','me','library',id,...(status?['status']:[])];
 const response=await proxyRequest(new Request('http://web.test/api/v1/'+path.join('/'),{method:status?'PATCH':'PUT',headers:{Origin:'http://web.test','X-CSRF-TOKEN':'token','Content-Type':'application/json','X-RateFreak-Account-Id':id},body:JSON.stringify({status:'completed',expectedStatus:'planned'})}),path);
 expect(response.status).toBe(200);expect(new Headers(upstream.mock.calls.at(-1)?.[1]?.headers).get('X-RateFreak-Account-Id')).toBe(status?id:null);
 }
});
it('requires same-origin CSRF on status writes before upstream',async()=>{
 const path=['users','me','library',id,'status'];const response=await proxyRequest(new Request('http://web.test/api/v1/'+path.join('/'),{method:'PATCH',headers:{Origin:'http://foreign.test','Content-Type':'application/json'},body:'{}'}),path);
 expect(response.status).toBe(403);expect(upstream).not.toHaveBeenCalled();
});
