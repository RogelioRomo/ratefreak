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
