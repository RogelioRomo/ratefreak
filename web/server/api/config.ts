import "server-only";
import { isIP } from "node:net";
import type { ApiProxyConfig } from "@/server/interfaces/api-proxy-config";

function readOrigin(value: string | undefined): string {
  try {
    const url = new URL(value ?? "");
    if (!value || !["http:", "https:"].includes(url.protocol) || url.username || url.password || url.pathname !== "/" || url.search || url.hash) throw new Error();
    return url.origin;
  } catch {
    throw new Error("API proxy is not configured.");
  }
}

function privateHost(hostname: string): boolean {
  const host = hostname.replace(/^\[|\]$/g, "");
  if (isIP(host) === 4) {
    const [a, b] = host.split(".").map(Number);
    return a === 127 || a === 10 || (a === 172 && b >= 16 && b <= 31) || (a === 192 && b === 168);
  }
  if (isIP(host) === 6) return host === "::1" || /^f[cd][0-9a-f]{2}:/i.test(host);
  return /^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/i.test(host);
}

export function readApiProxyConfig(): ApiProxyConfig {
  const prefix = process.env.NODE_ENV === "development" ? "" : "__Host-";
  const apiOrigin = readOrigin(process.env.RATEFREAK_API_URL);
  const proxySecret = process.env.RATEFREAK_PROXY_SECRET;
  const clientIpMode = process.env.RATEFREAK_CLIENT_IP_MODE;
  const ingressSecret = process.env.RATEFREAK_INGRESS_SECRET;
  const privateHttp = process.env.RATEFREAK_ALLOW_PRIVATE_API_HTTP;
  const allowPrivateApiHttp = privateHttp === "true";
  const webOrigin = process.env.RATEFREAK_WEB_ORIGIN ? readOrigin(process.env.RATEFREAK_WEB_ORIGIN) : undefined;
  const strong = (value: string | undefined) => /^[a-fA-F0-9]{64}$/.test(value ?? "");
  if (proxySecret || clientIpMode || ingressSecret || privateHttp) {
    const local = ["localhost", "127.0.0.1", "[::1]"].includes(new URL(apiOrigin).hostname);
    if (!/^[a-fA-F0-9]{64}$/.test(proxySecret ?? "") ||
      (clientIpMode !== "vercel" && clientIpMode !== "loopback" && clientIpMode !== "trusted-proxy") ||
      (clientIpMode === "loopback" && (process.env.NODE_ENV !== "development" || !local)) ||
      (clientIpMode === "vercel" && (process.env.VERCEL !== "1" || !apiOrigin.startsWith("https://"))) ||
      (privateHttp !== undefined && privateHttp !== "true" && privateHttp !== "false") ||
      (allowPrivateApiHttp && clientIpMode !== "trusted-proxy") ||
      (clientIpMode === "trusted-proxy" && (!strong(ingressSecret) ||
        ingressSecret!.toLowerCase() === proxySecret!.toLowerCase() || !webOrigin?.startsWith("https://") ||
        (!apiOrigin.startsWith("https://") && !(allowPrivateApiHttp && privateHost(new URL(apiOrigin).hostname)))))) {
      throw new Error("API proxy is not configured.");
    }
  }
  return {
    apiOrigin,
    webOrigin,
    cookieNames: [`${prefix}ratefreak.session`, `${prefix}ratefreak.csrf`, `${process.env.NODE_ENV === "development" ? "" : "__Secure-"}ratefreak.lifecycle`],
    timeoutMs: 10_000,
    proxySecret,
    clientIpMode: clientIpMode as ApiProxyConfig["clientIpMode"],
    ingressSecret,
    allowPrivateApiHttp,
  };
}
