import { expect, it, vi } from "vitest";
import { clientAttributionHeaders } from "./client-attribution";
import { readApiProxyConfig } from "./config";

const secret = "a".repeat(64);
function configuration(mode: string) {
  vi.stubEnv("RATEFREAK_API_URL", "https://api.test");
  vi.stubEnv("RATEFREAK_PROXY_SECRET", secret);
  vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", mode);
  return readApiProxyConfig();
}

it("uses platform IP and server key, ignoring forged caller service headers", () => {
  vi.stubEnv("NODE_ENV", "production");
  vi.stubEnv("VERCEL", "1");
  const config = configuration("vercel");
  const request = new Request("https://web.test", { headers: {
    "x-vercel-forwarded-for": "192.0.2.1", "X-RateFreak-Proxy-Key": "forged",
    "X-RateFreak-Client-IP": "192.0.2.99", "X-Forwarded-For": "192.0.2.99",
  } });
  const headers = clientAttributionHeaders(request, config);
  expect(headers.get("X-RateFreak-Proxy-Key")).toBe(secret);
  expect(headers.get("X-RateFreak-Client-IP")).toBe("192.0.2.1");
  expect(headers.has("X-Forwarded-For")).toBe(false);
});

it.each([null, "not-an-ip", "192.0.2.1, 192.0.2.2"])("rejects missing or ambiguous platform IP %s", (ip) => {
  vi.stubEnv("NODE_ENV", "production"); vi.stubEnv("VERCEL", "1");
  const config = configuration("vercel");
  const request = new Request("https://web.test", { headers: ip ? { "x-vercel-forwarded-for": ip } : {} });
  expect(() => clientAttributionHeaders(request, config)).toThrow();
});

it("does not trust Vercel headers outside Vercel runtime", () => {
  vi.stubEnv("NODE_ENV", "production"); vi.stubEnv("VERCEL", undefined);
  expect(() => configuration("vercel")).toThrow();
});

it("permits loopback only for Development and a loopback upstream", () => {
  vi.stubEnv("NODE_ENV", "production");
  expect(() => configuration("loopback")).toThrow();
  vi.stubEnv("NODE_ENV", "development");
  expect(() => configuration("loopback")).toThrow();
  vi.stubEnv("RATEFREAK_API_URL", "http://localhost:5080");
  expect(clientAttributionHeaders(new Request("http://localhost:3000"), readApiProxyConfig()).get("X-RateFreak-Client-IP")).toBe("127.0.0.1");
});

it("requires a complete, strong service configuration and HTTPS in production", () => {
  vi.stubEnv("NODE_ENV", "production"); vi.stubEnv("VERCEL", "1");
  configuration("vercel");
  vi.stubEnv("RATEFREAK_API_URL", "http://api.test");
  expect(() => readApiProxyConfig()).toThrow();
  vi.stubEnv("RATEFREAK_API_URL", "https://api.test");
  vi.stubEnv("RATEFREAK_PROXY_SECRET", "short");
  expect(() => readApiProxyConfig()).toThrow();
});
