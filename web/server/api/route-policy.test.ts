import { expect, it } from "vitest";
import { isAllowedRoute } from "./route-policy";

it("permits only the two exact GET routes", () => {
  expect(isAllowedRoute("GET", ["auth", "csrf"])).toBe(true);
  expect(isAllowedRoute("GET", ["users", "me"])).toBe(true);
  expect(isAllowedRoute("POST", ["auth", "csrf"])).toBe(false);
});

it.each([
  [], ["users"], ["users", "me", "extra"], ["auth", "login"],
  ["..", "users", "me"], ["users%2fme"], ["users\\me"], ["https:", "evil.test"],
].map((path) => [path]))("rejects unapproved path %j", (path) => {
  expect(isAllowedRoute("GET", path)).toBe(false);
});

it("permits exact profile lookup and owner update routes only", () => {
 expect(isAllowedRoute("PATCH", ["users", "me"])).toBe(true);
 expect(isAllowedRoute("GET", ["users", "by-handle", "Test_user"])).toBe(true);
 for (const handle of ["ab", "a".repeat(31), "a-b", "a%2fb", "../me"]) expect(isAllowedRoute("GET", ["users", "by-handle", handle])).toBe(false);
 expect(isAllowedRoute("PATCH", ["users", "by-handle", "owner"])).toBe(false);
 expect(isAllowedRoute("POST", ["users", "me"])).toBe(false);
});

it('permits only exact report POST routes', () => {
 const id='a1111111-1111-1111-1111-111111111111';
 for (const segment of ['reviews','comments']) {
  expect(isAllowedRoute('POST',[segment,id,'reports'])).toBe(true);
  for(const method of ['GET','PATCH','DELETE','PUT']) expect(isAllowedRoute(method,[segment,id,'reports'])).toBe(false);
  expect(isAllowedRoute('POST',[segment,'bad','reports'])).toBe(false);
  expect(isAllowedRoute('POST',[segment,id,'reports','extra'])).toBe(false);
 }
 expect(isAllowedRoute('POST',['moderation','reports',id,'resolve'])).toBe(false);
});
