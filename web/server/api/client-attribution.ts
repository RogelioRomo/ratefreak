import "server-only";
import { timingSafeEqual } from "node:crypto";
import { isIP } from "node:net";
import type { ApiProxyConfig } from "@/server/interfaces/api-proxy-config";

export function clientAttributionHeaders(request: Request, config: ApiProxyConfig): Headers {
  const headers = new Headers();
  if (!config.proxySecret || !config.clientIpMode) return headers;
  let ip: string | null;
  if (config.clientIpMode === "trusted-proxy") {
    const key = request.headers.get("X-RateFreak-Ingress-Key");
    const expected = config.ingressSecret;
    if (!key || !expected || !/^[a-fA-F0-9]{64}$/.test(key) ||
      Buffer.byteLength(key) !== Buffer.byteLength(expected) ||
      !timingSafeEqual(Buffer.from(key), Buffer.from(expected))) throw new Error("Client attribution is unavailable.");
    ip = request.headers.get("X-RateFreak-Ingress-IP");
  } else {
    ip = config.clientIpMode === "loopback" ? "127.0.0.1" : request.headers.get("x-vercel-forwarded-for");
  }
  if (!ip || ip.includes("%") || !isIP(ip)) throw new Error("Client attribution is unavailable.");
  headers.set("X-RateFreak-Proxy-Key", config.proxySecret);
  headers.set("X-RateFreak-Client-IP", ip);
  return headers;
}
