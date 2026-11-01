import { expect, it } from "vitest";
import { parseFilters, catalogHref, submittedFilters } from "./filters";

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
