import "server-only";
import { isTmdbPosterFilename, tmdbPosterOrigin } from "@/shared/artwork/tmdb-poster-paths";
import { publicArtworkRequest } from "./public-artwork-request";

export const posterBytesLimit = 5 * 1024 * 1024;
export const posterTypes = ["image/jpeg", "image/png", "image/webp", "image/avif"] as const;
export type PosterType = typeof posterTypes[number];
export type TmdbPosterPicture = { type: PosterType; bytes: Buffer<ArrayBuffer> };

const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
export const posterSignatureBytes = 64;

export function hasPosterSignature(type: PosterType, bytes: Buffer): boolean {
  switch (type) {
    case "image/jpeg":
      return bytes.length >= 4 && bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff;
    case "image/png":
      return bytes.subarray(0, 8).equals(pngSignature);
    case "image/webp":
      return bytes.toString("ascii", 0, 4) === "RIFF" && bytes.toString("ascii", 8, 12) === "WEBP";
    case "image/avif": {
      if (bytes.length < 16 || bytes.toString("ascii", 4, 8) !== "ftyp") return false;
      const brand = (offset: number) => ["avif", "avis"].includes(bytes.toString("ascii", offset, offset + 4));
      const boxSize = bytes.readUInt32BE(0);
      if (boxSize < 16) return false;
      if (brand(8)) return true;
      for (let offset = 16; offset + 4 <= Math.min(boxSize, posterSignatureBytes, bytes.length); offset += 4) {
        if (brand(offset)) return true;
      }
      return false;
    }
  }
}

export async function fetchTmdbPoster(filename: string, signal: AbortSignal): Promise<TmdbPosterPicture | null> {
  if (!isTmdbPosterFilename(filename)) throw new Error("Invalid TMDB poster source.");
  const response = await publicArtworkRequest(new URL(tmdbPosterOrigin + filename), signal, true);
  try {
    if (response.statusCode === 404) return null;
    const type = response.headers["content-type"]?.split(";")[0].trim().toLowerCase();
    const supportedType = posterTypes.find(candidate => candidate === type);
    const contentLength = response.headers["content-length"];
    const encoding = response.headers["content-encoding"];
    if (response.statusCode !== 200 || !supportedType || (encoding && encoding !== "identity") || (
      contentLength !== undefined && (!/^\d+$/.test(contentLength) || Number(contentLength) > posterBytesLimit)
    )) throw new Error("Unexpected TMDB image response.");

    const chunks: Buffer[] = [];
    let size = 0;
    for await (const chunk of response) {
      const bytes = Buffer.from(chunk as Uint8Array);
      size += bytes.length;
      if (size > posterBytesLimit) throw new Error("TMDB image exceeds its size limit.");
      chunks.push(bytes);
    }
    signal.throwIfAborted();
    const bytes = Buffer.concat(chunks, size);
    if ((contentLength !== undefined && size !== Number(contentLength)) || !hasPosterSignature(supportedType, bytes)) {
      throw new Error("Invalid TMDB image bytes.");
    }
    return { type: supportedType, bytes };
  } finally {
    response.destroy();
  }
}
