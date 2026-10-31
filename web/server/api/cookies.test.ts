import { expect, it } from "vitest";
import { selectCookies } from "./cookies";

it("forwards only exact approved cookie names", () => {
  expect(selectCookies("other=secret; ratefreak.session=s; ratefreak.csrf=c; ratefreak.session.fake=x; oauth=y", ["ratefreak.session", "ratefreak.csrf"]))
    .toBe("ratefreak.session=s; ratefreak.csrf=c");
  expect(selectCookies(null, ["ratefreak.session"])).toBe("");
});

it("rejects ambiguous duplicate session cookies", () => {
  expect(() => selectCookies("ratefreak.session=a; ratefreak.session=b", ["ratefreak.session"])).toThrow();
});
