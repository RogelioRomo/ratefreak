import { expect, it } from "vitest";
import { safePreviewReturnTo } from "./return-to";
it('accepts only canonical internal preview paths',()=>expect(safePreviewReturnTo('/catalog/discover/tmdb/movie/11')).toBe('/catalog/discover/tmdb/movie/11'));
it.each([null,'https://evil.test','//evil.test','/account','/catalog/discover/tmdb/movie/0011','/catalog/discover/tmdb/movie/11?x=y','/catalog/discover/tmdb/album/11','/%63atalog/discover/tmdb/movie/11','/catalog/discover/tmdb/movie/11#x'])('rejects continuation %s',x=>expect(safePreviewReturnTo(x)).toBeNull());
it.each(['/library','/catalog/a1111111-1111-1111-1111-111111111111'])('accepts approved personal continuation %s',x=>expect(safePreviewReturnTo(x)).toBe(x));
it.each(['/library?status=planned','/catalog/not-guid','/catalog/A1111111-1111-1111-1111-111111111111','/library#x'])('rejects ambiguous personal continuation %s',x=>expect(safePreviewReturnTo(x)).toBeNull());
it.each(['/collections','/collections/new','/collections/a1111111-1111-1111-1111-111111111111'])('accepts canonical collection continuation %s',x=>expect(safePreviewReturnTo(x)).toBe(x));
it.each(['/collections?scope=mine','/collections/new#x','/collections/A1111111-1111-1111-1111-111111111111','/collections/not-guid','/%63ollections','//collections','/collections/new/'])('rejects ambiguous collection continuation %s',x=>expect(safePreviewReturnTo(x)).toBeNull());
