import { describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/api/api-error";
import { createQueryClient } from "./create-query-client";

describe("query policy", () => {
  it("keeps fresh data for one minute and isolates independent clients", async () => {
    const first = createQueryClient();
    const second = createQueryClient();
    try {
      first.setQueryData(["example"], "cached");
      const load = vi.fn().mockResolvedValue("fetched");
      expect(await first.fetchQuery({ queryKey: ["example"], queryFn: load })).toBe("cached");
      expect(load).not.toHaveBeenCalled();
      expect(second.getQueryData(["example"])).toBeUndefined();
      expect(first.getDefaultOptions()).toMatchObject({ queries: { staleTime: 60_000, gcTime: 300_000, refetchOnWindowFocus: true }, mutations: { retry: false } });
    } finally { first.clear(); second.clear(); }
  });

  it.each([new TypeError("Network error"), new ApiError(503)])("retries transient failure once: %s", async (error) => {
    const client = createQueryClient();
    const load = vi.fn().mockRejectedValue(error);
    try {
      await expect(client.fetchQuery({ queryKey: ["retry"], queryFn: load, retryDelay: 0 })).rejects.toBe(error);
      expect(load).toHaveBeenCalledTimes(2);
    } finally { client.clear(); }
  });

  it.each([401, 403, 404, 429])("does not retry HTTP %i", async (status) => {
    const client = createQueryClient();
    const load = vi.fn().mockRejectedValue(new ApiError(status));
    try {
      await expect(client.fetchQuery({ queryKey: [status], queryFn: load, retryDelay: 0 })).rejects.toMatchObject({ status });
      expect(load).toHaveBeenCalledTimes(1);
    } finally { client.clear(); }
  });

  it.each([new Error("Programming error"), new DOMException("Cancelled", "AbortError")])("does not retry permanent or cancelled failures: %s", async (error) => {
    const client = createQueryClient();
    const load = vi.fn().mockRejectedValue(error);
    try {
      await expect(client.fetchQuery({ queryKey: ["permanent"], queryFn: load, retryDelay: 0 })).rejects.toBe(error);
      expect(load).toHaveBeenCalledTimes(1);
    } finally { client.clear(); }
  });
});
