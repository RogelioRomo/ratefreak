export interface ApiProxyConfig {
  apiOrigin: string;
  webOrigin?: string;
  cookieNames: readonly string[];
  timeoutMs: number;
}
