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

function trustedConfiguration(api = "http://api:8080") {
  vi.stubEnv("NODE_ENV", "production");
  vi.stubEnv("RATEFREAK_API_URL", api);
  vi.stubEnv("RATEFREAK_WEB_ORIGIN", "https://ratefreak.test");
  vi.stubEnv("RATEFREAK_PROXY_SECRET", secret);
  vi.stubEnv("RATEFREAK_INGRESS_SECRET", "b".repeat(64));
  vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "trusted-proxy");
  vi.stubEnv("RATEFREAK_ALLOW_PRIVATE_API_HTTP", "true");
  return readApiProxyConfig();
}

it.each(["192.0.2.7", "2001:db8::7"])("authenticates self-hosted ingress address %s and constructs only API attribution", ip => {
  const config = trustedConfiguration();
  const request = new Request("https://ratefreak.test", { headers: {
    "X-RateFreak-Ingress-Key": "b".repeat(64), "X-RateFreak-Ingress-IP": ip,
    "X-Forwarded-For": "192.0.2.99", "x-vercel-forwarded-for": "192.0.2.99",
    "X-RateFreak-Proxy-Key": "forged", "X-RateFreak-Client-IP": "192.0.2.99",
  }});
  expect([...clientAttributionHeaders(request, config)]).toEqual([
    ["x-ratefreak-client-ip", ip], ["x-ratefreak-proxy-key", secret],
  ]);
});

it.each([
  [undefined, "192.0.2.7"], ["c".repeat(64), "192.0.2.7"],
  ["b".repeat(64) + ", " + "b".repeat(64), "192.0.2.7"],
  ["b".repeat(64), undefined], ["b".repeat(64), "192.0.2.7, 192.0.2.8"],
  ["b".repeat(64), "not-an-ip"], ["b".repeat(64), "fe80::1%eth0"],
])("rejects untrusted or combined ingress credentials/address (%s, %s)", (key, ip) => {
  const config = trustedConfiguration();
  const headers = new Headers({ "X-Forwarded-For": "192.0.2.99" });
  if (key) headers.set("X-RateFreak-Ingress-Key", key);
  if (ip) headers.set("X-RateFreak-Ingress-IP", ip);
  expect(() => clientAttributionHeaders(new Request("https://ratefreak.test", {headers}), config)).toThrow();
});

it("rejects combined repeated ingress headers", () => {
  const config = trustedConfiguration();
  const headers = new Headers({"X-RateFreak-Ingress-Key": "b".repeat(64), "X-RateFreak-Ingress-IP": "192.0.2.7"});
  headers.append("X-RateFreak-Ingress-IP", "192.0.2.8");
  expect(() => clientAttributionHeaders(new Request("https://ratefreak.test", {headers}), config)).toThrow();
});

it.each(["http://api.example.com", "http://8.8.8.8", "http://[2001:4860:4860::8888]"])("rejects public plaintext upstream %s", api => {
  expect(() => trustedConfiguration(api)).toThrow();
});

it.each(["http://api:8080", "http://10.1.2.3:8080", "http://172.16.0.2", "http://192.168.1.2", "http://[fd00::1]"])("requires explicit plaintext opt-in for private upstream %s", api => {
  trustedConfiguration(api);
  vi.stubEnv("RATEFREAK_ALLOW_PRIVATE_API_HTTP", undefined);
  expect(() => readApiProxyConfig()).toThrow();
});

it("requires separate strong ingress/API secrets and an HTTPS browser origin", () => {
  trustedConfiguration();
  vi.stubEnv("RATEFREAK_INGRESS_SECRET", secret);
  expect(() => readApiProxyConfig()).toThrow();
  vi.stubEnv("RATEFREAK_INGRESS_SECRET", "short");
  expect(() => readApiProxyConfig()).toThrow();
  vi.stubEnv("RATEFREAK_INGRESS_SECRET", "b".repeat(64));
  vi.stubEnv("RATEFREAK_WEB_ORIGIN", "http://ratefreak.test");
  expect(() => readApiProxyConfig()).toThrow();
});
