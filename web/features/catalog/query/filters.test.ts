import { expect, it } from "vitest";
import { parseFilters, catalogHref, submittedFilters, filterParams } from "./filters";

it("defaults to first page with twenty results", () => {
  expect(parseFilters(new URLSearchParams())).toEqual({ q: "", type: "", page: 1, pageSize: 20 });
});
it.each(["q=a&q=b", "page=-1", "page=10001", "type=book", "type=constructor", "type=toString", "pageSize=50", "x=y", "q=%00"])("rejects invalid URL %s", (value) => {
  expect(parseFilters(new URLSearchParams(value))).toBeNull();
});
it("normalizes filters, resets page and encodes links", () => {
  const filters = submittedFilters("  A & B  ", "album");
  expect(filters).toEqual({ q: "A & B", type: "album", page: 1, pageSize: 20 });
  expect(catalogHref({ ...filters, page: 2 })).toBe("/catalog?q=A+%26+B&type=album&page=2");
});

it("keeps category filters independent of legacy media type filters", () => {
  const categoryId = "a1111111-1111-1111-1111-111111111111";
  const filters = parseFilters(new URLSearchParams(`categoryId=${categoryId}&type=custom`));
  expect(filters).toMatchObject({ categoryId, type: "custom" });
  expect(filterParams(filters!, true).get("categoryId")).toBe(categoryId);
});
