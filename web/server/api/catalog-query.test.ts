import { describe, expect, it } from "vitest";
import { validateCatalogQuery } from "./catalog-query";

describe("catalog query boundary", () => {
  it("trims and canonically encodes search", () => {
    expect(validateCatalogQuery(new URLSearchParams("q=++film+%26+music++&type=movie&page=2&pageSize=20")).toString()).toBe("q=film+%26+music&type=movie&page=2&pageSize=20");
  });
  it.each(["movie", "tv_show", "album", "video_game"])("accepts %s", (type) => {
    expect(validateCatalogQuery(new URLSearchParams({ type })).get("type")).toBe(type);
  });
  it.each(["q=a&q=b", "target=x", "type=Movie", "page=0", "page=10001", "page=1.2", "page=1e2", "pageSize=0", "pageSize=101", "q=%00", `q=${"a".repeat(201)}`])("rejects %s", (query) => {
    expect(() => validateCatalogQuery(new URLSearchParams(query))).toThrow();
  });
  it("accepts inclusive bounds and an empty query", () => {
    expect(validateCatalogQuery(new URLSearchParams()).toString()).toBe("");
    expect(validateCatalogQuery(new URLSearchParams("page=10000&pageSize=100")).get("page")).toBe("10000");
  });
});
