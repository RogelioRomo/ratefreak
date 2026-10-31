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
  return {
    apiOrigin: readOrigin(process.env.RATEFREAK_API_URL),
    webOrigin: process.env.RATEFREAK_WEB_ORIGIN ? readOrigin(process.env.RATEFREAK_WEB_ORIGIN) : undefined,
    cookieNames: [`${prefix}ratefreak.session`, `${prefix}ratefreak.csrf`],
    timeoutMs: 10_000,
  };
}
