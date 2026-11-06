import { beforeEach, expect, it, vi } from "vitest";
import { proxyRequest } from "./proxy";
const upstream = vi.fn<typeof fetch>();
beforeEach(() => {
  vi.stubEnv("NODE_ENV","development"); vi.stubEnv("RATEFREAK_API_URL","http://localhost:5080");
  vi.stubEnv("RATEFREAK_WEB_ORIGIN","http://web.test"); vi.stubEnv("RATEFREAK_CLIENT_IP_MODE","loopback"); vi.stubEnv("RATEFREAK_PROXY_SECRET","a".repeat(64));
  upstream.mockReset(); vi.stubGlobal("fetch",upstream);
});
it("forwards public setup status without caching",async()=>{
  upstream.mockResolvedValue(Response.json({ required:true,configured:true }));
  const response=await proxyRequest(new Request("http://web.test/api/v1/setup/status"),["setup","status"]);
  expect(response.status).toBe(200); expect(response.headers.get("cache-control")).toBe("no-store");
  expect(await response.json()).toEqual({required:true,configured:true});
});
it("forwards setup credentials only through origin/CSRF guarded POST",async()=>{
  upstream.mockResolvedValue(Response.json({ recoveryCode:"saved-code" },{status:201}));
  const body=JSON.stringify({setupSecret:"deployment-secret",password:"account-password"});
  const request=new Request("http://web.test/api/v1/setup",{method:"POST",headers:{Origin:"http://web.test","Content-Type":"application/json","X-CSRF-TOKEN":"csrf"},body});
  const response=await proxyRequest(request,["setup"]);expect(response.status).toBe(201);expect(response.headers.get("cache-control")).toBe("no-store");
  expect(new TextDecoder().decode(upstream.mock.calls[0][1]?.body as Uint8Array)).toBe(body);
  upstream.mockClear();
  const denied=await proxyRequest(new Request("http://web.test/api/v1/setup",{method:"POST",headers:{Origin:"https://evil.test"},body}),["setup"]);
  expect(denied.status).toBe(403);expect(upstream).not.toHaveBeenCalled();
});
it.each([['setup','GET',405],['setup/status','POST',405],['setup?secret=x','POST',404],['setup/extra','POST',404]])("rejects %s with method %s",async(path,method,status)=>{
  const response=await proxyRequest(new Request(`http://web.test/api/v1/${path}`,{method}),path.split('?')[0].split('/'));
  expect(response.status).toBe(status);expect(upstream).not.toHaveBeenCalled();
});
