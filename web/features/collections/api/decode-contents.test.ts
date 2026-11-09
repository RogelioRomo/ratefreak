import { expect, it } from "vitest";
import { decodeItem, decodeDetails } from "./decode-contents";
import { details } from "../components/test-support";
const item = { id: "membership", mediaItemId: "catalog", media: { id: "catalog", type: "custom", title: "Hydrangea", releaseDate: null, imageReference: null }, position: 0, createdAt: "date", updatedAt: "date" };
it("decodes a referenced live item summary without duplicated field data", () => { expect(decodeItem(JSON.stringify(item))).toEqual(item); expect(decodeDetails(JSON.stringify(details))).toEqual(details); });
it("rejects unsafe revisions and mismatched item identities", () => {
 expect(() => decodeDetails(JSON.stringify(details).replace('"revision":1', '"revision":9007199254740993'))).toThrow();
 expect(() => decodeItem(JSON.stringify({ ...item, mediaItemId: null }))).toThrow(); expect(() => decodeItem(JSON.stringify({ ...item, mediaItemId: "different" }))).toThrow();
});
