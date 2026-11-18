import { describe, expect, it } from "vitest";
import { isAllowedRoute } from "./route-policy";
import { proxyRequest } from "./proxy";
import { notificationQuery } from "./notification-policy";

describe("notification routes",()=>{
 it("allows only the inbox, count and owned mutations",()=>{
  for(const [method,path] of [["GET","notifications"],["GET","notifications/unread-count"],["DELETE","notifications"],["POST","notifications/read-all"],["DELETE","notifications/11111111-1111-1111-1111-111111111111"],["PUT","notifications/11111111-1111-1111-1111-111111111111/read"],["DELETE","notifications/11111111-1111-1111-1111-111111111111/read"]]) expect(isAllowedRoute(method,path.split("/"))).toBe(true);
  for(const [method,path] of [["POST","notifications"],["GET","notifications/11111111-1111-1111-1111-111111111111"],["DELETE","notifications/unread-count"],["PUT","notifications/bad/read"],["GET","users/me/notifications"]]) expect(isAllowedRoute(method,path.split("/"))).toBe(false);
 });
 it("validates bounded paging and rejects recipient overrides and duplicate keys",()=>{
  expect(notificationQuery(new URLSearchParams("page=2&pageSize=20")).toString()).toBe("page=2&pageSize=20");
  for(const q of ["page=0","page=10001","pageSize=101","recipient=other","page=1&page=2","pageSize=0"]) expect(()=>notificationQuery(new URLSearchParams(q))).toThrow();
 });
 it("rejects query parameters on clear and count before forwarding",async()=>{
  for(const [method,path] of [["DELETE","notifications"],["GET","notifications/unread-count"]]) {
   const r=await proxyRequest(new Request(`https://example.test/api/v1/${path}?page=1`,{method}),path.split("/")); expect(r.status).toBe(404);
  }
 });
});

it("rejects unexpected or malformed clear bodies through the actual proxy",async()=>{
 const {vi}=await import("vitest");
 vi.stubEnv("NODE_ENV","development");vi.stubEnv("RATEFREAK_API_URL","http://localhost:5081");vi.stubEnv("RATEFREAK_WEB_ORIGIN","https://example.test");vi.stubEnv("RATEFREAK_PROXY_SECRET","a".repeat(64));vi.stubEnv("RATEFREAK_CLIENT_IP_MODE","loopback");
 const fetcher=vi.fn(async()=>new Response(null,{status:204}));vi.stubGlobal("fetch",fetcher);
 for(const body of ['{"id":"one"}','{','[]','null']){
  const r=await proxyRequest(new Request("https://example.test/api/v1/notifications",{method:"DELETE",headers:{Origin:"https://example.test","Content-Type":"application/json","X-CSRF-TOKEN":"token"},body}),["notifications"]);
  expect(r.status).toBe(400);
 }
 expect(fetcher).not.toHaveBeenCalled();
});
