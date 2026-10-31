export interface ApiProxyConfig {
  apiOrigin: string;
  webOrigin?: string;
  cookieNames: readonly string[];
  timeoutMs: number;
  proxySecret?: string;
  clientIpMode?: "vercel" | "loopback";
}
