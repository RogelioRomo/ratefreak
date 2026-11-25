import { expect, it } from "vitest";
import { hasRemoteMatch } from "next/dist/shared/lib/match-remote-pattern";
import { hasLocalMatch } from "next/dist/shared/lib/match-local-pattern";
import config from "../next.config";

it.each(["https://images.igdb.com/igdb/image/upload/t_cover_big/co123.jpg"])("permits the canonical provider artwork %s", url => {
  expect(hasRemoteMatch([], config.images?.remotePatterns ?? [], new URL(url))).toBe(true);
});
it.each([
  "https://image.tmdb.org/t/p/w500/example.jpg",
  "http://image.tmdb.org/t/p/w500/example.jpg",
  "https://image.tmdb.org/t/p/original/example.jpg",
  "https://image.tmdb.org/t/p/w500/example.jpg?target=http://127.0.0.1",
  "https://images.igdb.com/igdb/image/upload/t_original/co123.jpg",
  "https://covers.openlibrary.org/b/isbn/9780140328721-L.jpg?default=false",
  "https://covers.openlibrary.org/b/id/12345-L.jpg?default=false",
  "https://covers.openlibrary.org/b/id/12345-L.jpg?redirect=https://evil.test",
  "https://evil.test/image.jpg",
  "https://127.0.0.1/private.jpg",
])("rejects noncanonical optimizer sources %s", url => {
  expect(hasRemoteMatch([], config.images?.remotePatterns ?? [], new URL(url))).toBe(false);
});
it("permits only the dedicated local cover adapter without query strings", () => {
  expect(hasLocalMatch(config.images?.localPatterns, "/provider-artwork/openlibrary/12345")).toBe(true);
  expect(hasLocalMatch(config.images?.localPatterns, "/provider-artwork/openlibrary/12345?url=http://127.0.0.1")).toBe(false);
});
it.each(["/api/v1/media/a/artwork/b", "/api/v1/moderation/reports/a/artwork/b", "/tmdb-posters/example.jpg", "/private.jpg"])("blocks direct optimizer access to local source %s", path => {
  expect(hasLocalMatch(config.images?.localPatterns, path)).toBe(false);
});
