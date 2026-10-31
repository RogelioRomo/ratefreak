import "server-only";
import { isIP } from "node:net";
import type { ApiProxyConfig } from "@/server/interfaces/api-proxy-config";

export function clientAttributionHeaders(request: Request, config: ApiProxyConfig): Headers {
  const headers = new Headers();
  if (!config.proxySecret || !config.clientIpMode) return headers;
  const ip = config.clientIpMode === "loopback" ? "127.0.0.1" : request.headers.get("x-vercel-forwarded-for");
  if (!ip || !isIP(ip)) throw new Error("Client attribution is unavailable.");
  headers.set("X-RateFreak-Proxy-Key", config.proxySecret);
  headers.set("X-RateFreak-Client-IP", ip);
  return headers;
}
