export interface ApiProxyConfig {
  apiOrigin: string;
  webOrigin?: string;
  cookieNames: readonly string[];
  timeoutMs: number;
  proxySecret?: string;
  ingressSecret?: string;
  allowPrivateApiHttp?: boolean;
  clientIpMode?: "vercel" | "loopback" | "trusted-proxy";
}
