import "server-only";
import { lookup } from "node:dns/promises";
import { request } from "node:https";
import type { IncomingMessage } from "node:http";
import { isIP, type LookupFunction } from "node:net";
import { isPrivateIp } from "next/dist/server/is-private-ip";

export async function withAbort<T>(work: Promise<T>, signal: AbortSignal): Promise<T> {
  let abort!: () => void;
  const interrupted = new Promise<never>((_resolve, reject) => {
    abort = () => reject(signal.reason);
    if (signal.aborted) abort();
    else signal.addEventListener("abort", abort, { once: true });
  });
  return Promise.race([work, interrupted]).finally(() => signal.removeEventListener("abort", abort));
}

export async function publicArtworkRequest(url: URL, signal: AbortSignal, revalidate = false): Promise<IncomingMessage> {
  signal.throwIfAborted();
  const addresses = await withAbort(lookup(url.hostname, { all: true }), signal);
  if (!addresses.length || addresses.some(({ address, family }) => (
    !isIP(address) || isIP(address) !== family || isPrivateIp(address)
    || (family === 6 && (!/^[23]/.test(address) || address.includes("%")))
  ))) throw new Error("Invalid image address.");

  const pinned: LookupFunction = (hostname, options, callback) => {
    if (hostname !== url.hostname) return callback(new Error("Unexpected image hostname."), []);
    const matching = addresses.filter(address => !options.family || address.family === options.family);
    if (!matching.length) return callback(new Error("Image address unavailable."), []);
    if (options.all) callback(null, matching);
    else callback(null, matching[0].address, matching[0].family);
  };
  return new Promise((resolve, reject) => {
    const outgoing = request(url, {
      signal, lookup: pinned, agent: false,
      headers: {
        Accept: "image/jpeg,image/png,image/webp,image/avif",
        "User-Agent": "RateFreak/0.1",
        ...(revalidate ? { "Cache-Control": "no-cache" } : {}),
      },
    }, resolve);
    outgoing.on("error", reject);
    outgoing.end();
  });
}
