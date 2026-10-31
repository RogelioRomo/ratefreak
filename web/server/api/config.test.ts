import { describe, expect, it, vi } from "vitest";
import { readApiProxyConfig } from "./config";

describe("proxy configuration", () => {
  it.each([undefined, "", "not-a-url", "ftp://api.test", "https://u:p@api.test", "https://api.test/v1", "https://api.test?x=1", "https://api.test#x"])("rejects invalid API origin %s", (value) => {
    vi.stubEnv("RATEFREAK_API_URL", value);
    expect(() => readApiProxyConfig()).toThrow("API proxy is not configured.");
  });

  it("normalizes origins and selects production cookie names", () => {
    vi.stubEnv("RATEFREAK_API_URL", "https://api.test/");
    vi.stubEnv("NODE_ENV", "production");
    expect(readApiProxyConfig()).toMatchObject({
      apiOrigin: "https://api.test", timeoutMs: 10_000,
      cookieNames: ["__Host-ratefreak.session", "__Host-ratefreak.csrf"],
    });
  });

  it("selects local HTTP cookie names only in development", () => {
    vi.stubEnv("RATEFREAK_API_URL", "http://localhost:5080");
    vi.stubEnv("NODE_ENV", "development");
    expect(readApiProxyConfig().cookieNames).toEqual(["ratefreak.session", "ratefreak.csrf"]);
  });
});
