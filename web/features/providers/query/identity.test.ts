import { expect, it } from "vitest";
import { previewHref, providerIdentity } from "./identity";

it("uses canonical work identities for book previews", () => {
  const identity = providerIdentity("openlibrary", "book", "OL45804W");
  expect(identity).toEqual({ provider: "openlibrary", type: "book", externalId: "OL45804W" });
  expect(previewHref(identity!)).toBe("/catalog/discover/openlibrary/book/OL45804W");
});
it.each(["OL45804M", "9780140328721", "OL0W", "OL045804W", "/works/OL45804W", "ol45804w", "OL1W?x=1"])("rejects non-work identity %s", id => {
  expect(providerIdentity("openlibrary", "book", id)).toBeNull();
});
