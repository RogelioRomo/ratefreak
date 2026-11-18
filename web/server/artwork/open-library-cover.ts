import "server-only";
import { lookup } from "node:dns/promises";
import { request } from "node:https";
import type { IncomingMessage } from "node:http";
import type { LookupFunction } from "node:net";
import { isPrivateIp } from "next/dist/server/is-private-ip";
import { CoverFetchGate } from "./cover-fetch-gate";

const maximumBytes = 5 * 1024 * 1024;
const idPattern = /^[1-9][0-9]{0,9}$/;
const sourceFetches = new CoverFetchGate();

// The observed Open Library -> Archive -> archive storage chain. Each hop must
// refer to the same cover; public-domain allowlisting alone would be too broad.
export function allowedCoverUrl(url: URL, id: string): boolean {
  if (!idPattern.test(id) || url.protocol !== "https:" || url.username || url.password || url.port || url.hash) return false;
  if (url.hostname === "covers.openlibrary.org") return url.pathname === `/b/id/${id}-L.jpg` && url.search === "?default=false";
  const archivePath = new RegExp(`^/download/olcovers([0-9]+)/olcovers\\1-L\\.zip/${id}-L\\.jpg$`);
  if (url.hostname === "archive.org") return archivePath.test(url.pathname) && !url.search;
  if (!/^ia[0-9]{6}\.(?:us|eu)\.archive\.org$/.test(url.hostname) || url.pathname !== "/view_archive.php") return false;
  const params = url.searchParams;
  if ([...params.keys()].length !== 2 || params.getAll("archive").length !== 1 || params.getAll("file").length !== 1) return false;
  return /^\/[0-9]{1,3}\/items\/olcovers([0-9]+)\/olcovers\1-L\.zip$/.test(params.get("archive") ?? "") && params.get("file") === `${id}-L.jpg`;
}

async function publicRequest(url: URL, signal: AbortSignal): Promise<IncomingMessage> {
  signal.throwIfAborted();
  let abort!: () => void;
  const interrupted = new Promise<never>((_resolve, reject) => { abort = () => reject(signal.reason); signal.addEventListener("abort", abort, { once: true }); });
  const addresses = await Promise.race([lookup(url.hostname, { all: true }), interrupted]).finally(() => signal.removeEventListener("abort", abort));
  if (!addresses.length || addresses.some(address => isPrivateIp(address.address))) throw new Error("Invalid image address.");
  const pinned: LookupFunction = (hostname, options, callback) => {
    if (hostname !== url.hostname) return callback(new Error("Unexpected image hostname."), []);
    const matching = addresses.filter(address => !options.family || address.family === options.family);
    if (!matching.length) return callback(new Error("Image address unavailable."), []);
    if (options.all) callback(null, matching);
    else callback(null, matching[0].address, matching[0].family);
  };
  return new Promise((resolve, reject) => {
    const outgoing = request(url, { signal, lookup: pinned, agent: false, headers: { Accept: "image/jpeg,image/png,image/webp,image/avif", "User-Agent": "RateFreak/0.1" } }, resolve);
    outgoing.on("error", reject); outgoing.end();
  });
}

function problem(status: number): Response {
  return new Response("Cover unavailable.", { status, headers: { "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff", ...(status === 503 ? { "Retry-After": "1" } : {}) } });
}

export async function fetchOpenLibraryCover(id: string, caller: AbortSignal): Promise<Response> {
  if (!idPattern.test(id)) return problem(404);
  const signal = AbortSignal.any([caller, AbortSignal.timeout(10000)]);
  let url = new URL(`https://covers.openlibrary.org/b/id/${id}-L.jpg?default=false`);
  let release: (() => void) | undefined;
  try {
    const reservation = sourceFetches.acquire(signal);
    if (!reservation) return problem(503);
    release = await reservation;
    signal.throwIfAborted();
    for (let redirects = 0; redirects <= 3; redirects++) {
      if (!allowedCoverUrl(url, id)) return problem(502);
      const response = await publicRequest(url, signal);
      if ([301, 302, 303, 307, 308].includes(response.statusCode ?? 0)) {
        const location = response.headers.location; response.destroy();
        if (!location || redirects === 3) return problem(502);
        url = new URL(location, url); continue;
      }
      const type = response.headers["content-type"]?.split(";")[0].trim().toLowerCase();
      if (response.statusCode === 404) { response.destroy(); return problem(404); }
      if (response.statusCode !== 200 || !type || !["image/jpeg", "image/png", "image/webp", "image/avif"].includes(type) || Number(response.headers["content-length"] ?? 0) > maximumBytes) {
        response.destroy(); return problem(502);
      }
      const chunks: Buffer[] = []; let size = 0;
      for await (const chunk of response) {
        const bytes = Buffer.from(chunk); size += bytes.length;
        if (size > maximumBytes) { response.destroy(); return problem(502); }
        chunks.push(bytes);
      }
      if (!size) return problem(502);
      return new Response(Buffer.concat(chunks), { headers: { "Content-Type": type, "Cache-Control": "public, max-age=3600", "X-Content-Type-Options": "nosniff" } });
    }
  } catch { return problem(signal.aborted ? 504 : 502); }
  finally { release?.(); }
  return problem(502);
}
