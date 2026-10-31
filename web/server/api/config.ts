import "server-only";
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

export function readApiProxyConfig(): ApiProxyConfig {
  const prefix = process.env.NODE_ENV === "development" ? "" : "__Host-";
  const apiOrigin = readOrigin(process.env.RATEFREAK_API_URL);
  const proxySecret = process.env.RATEFREAK_PROXY_SECRET;
  const clientIpMode = process.env.RATEFREAK_CLIENT_IP_MODE;
  if (proxySecret || clientIpMode) {
    const local = ["localhost", "127.0.0.1", "[::1]"].includes(new URL(apiOrigin).hostname);
    if (!/^[a-fA-F0-9]{64}$/.test(proxySecret ?? "") ||
      (clientIpMode !== "vercel" && clientIpMode !== "loopback") ||
      (clientIpMode === "loopback" && (process.env.NODE_ENV !== "development" || !local)) ||
      (clientIpMode === "vercel" && (process.env.VERCEL !== "1" || !apiOrigin.startsWith("https://")))) {
      throw new Error("API proxy is not configured.");
    }
  }
  return {
    apiOrigin,
    webOrigin: process.env.RATEFREAK_WEB_ORIGIN ? readOrigin(process.env.RATEFREAK_WEB_ORIGIN) : undefined,
    cookieNames: [`${prefix}ratefreak.session`, `${prefix}ratefreak.csrf`],
    timeoutMs: 10_000,
    proxySecret,
    clientIpMode: clientIpMode as "vercel" | "loopback" | undefined,
  };
}
