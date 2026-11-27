export const igdbCoverOrigin = "https://images.igdb.com/igdb/image/upload/t_cover_big/";
export const igdbCoverRoute = "/igdb-covers/";

export function isIgdbCoverFilename(filename: unknown): filename is string {
  return typeof filename === "string" && filename.length <= 200
    && /^[A-Za-z0-9_-]+\.jpg(?![\s\S])/.test(filename);
}

export function igdbCoverFilename(reference: string): string | null {
  const filename = reference.startsWith(igdbCoverOrigin) ? reference.slice(igdbCoverOrigin.length) : null;
  return isIgdbCoverFilename(filename) ? filename : null;
}
