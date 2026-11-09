import type { CollectionDetails, CollectionPage, CollectionSummary } from "../interfaces/collections";
import type { CollectionItem, CollectionItemPage } from "../interfaces/contents";
import type { MediaSummary } from "@/features/catalog/interfaces/media-summary";
import { mediaTypes } from "@/features/catalog/query/filters";
const object = (value: unknown): Record<string, unknown> => { if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("Invalid object."); return value as Record<string, unknown>; };
const text = (value: unknown): string => { if (typeof value !== "string") throw new Error("Invalid text."); return value; };
const nullable = (value: unknown) => value === null ? null : text(value);
const integer = (value: unknown, min = 0, max = Number.MAX_SAFE_INTEGER): number => { if (typeof value !== "number" || !Number.isSafeInteger(value) || value < min || value > max) throw new Error("Invalid integer."); return value; };
const array = (value: unknown): unknown[] => { if (!Array.isArray(value)) throw new Error("Invalid array."); return value; };
function summary(value: unknown): CollectionSummary {
 const v = object(value), owner = object(v.owner); if (v.visibility !== "public" && v.visibility !== "private") throw new Error("Invalid visibility.");
 return { id: text(v.id).toLowerCase(), owner: { id: text(owner.id), handle: text(owner.handle), displayName: text(owner.displayName) }, name: text(v.name), description: nullable(v.description), visibility: v.visibility, revision: integer(v.revision, 1), createdAt: text(v.createdAt), updatedAt: text(v.updatedAt) };
}
function item(value: unknown): CollectionItem {
 const v = object(value), media = object(v.media); const type = text(media.type); if (!Object.hasOwn(mediaTypes, type)) throw new Error("Invalid item type.");
 const id = text(v.mediaItemId).toLowerCase(); if (text(media.id).toLowerCase() !== id) throw new Error("Inconsistent item reference.");
 return { id: text(v.id).toLowerCase(), mediaItemId: id, media: { id, type: type as MediaSummary["type"], title: text(media.title), releaseDate: nullable(media.releaseDate), imageReference: nullable(media.imageReference) }, position: integer(v.position, 0, 2147483647), createdAt: text(v.createdAt), updatedAt: text(v.updatedAt) };
}
export function decodeDetails(raw: string): CollectionDetails { const v = object(JSON.parse(raw)); return { collection: summary(v.collection) }; }
export function decodeCollectionPage(raw: string): CollectionPage { const v = object(JSON.parse(raw)); return { items: array(v.items).map(summary), page: integer(v.page, 1, 10000), pageSize: integer(v.pageSize, 1, 100), totalCount: integer(v.totalCount) }; }
export function decodeItem(raw: string): CollectionItem { return item(JSON.parse(raw)); }
export function decodeItems(raw: string): CollectionItemPage { const v = object(JSON.parse(raw)); return { items: array(v.items).map(item), page: integer(v.page, 1, 10000), pageSize: integer(v.pageSize, 1, 100), totalCount: integer(v.totalCount) }; }
