import { expect, it } from "vitest";
import { readRequestBody } from "./request-body";

it("accepts at most 65536 UTF-8 bytes", async () => {
  const request = new Request("http://web.test", { method: "POST", body: "é".repeat(32768) });
  expect((await readRequestBody(request)).byteLength).toBe(65536);
  await expect(readRequestBody(new Request("http://web.test", { method: "POST", body: "é".repeat(32769) }))).rejects.toMatchObject({ status: 413 });
});

it("bounds chunked bodies without Content-Length", async () => {
  let cancelled = false;
  const request = new Request("http://web.test", {
    method: "POST", body: new ReadableStream({
      pull(controller) { controller.enqueue(new Uint8Array(32769)); },
      cancel() { cancelled = true; },
    }), duplex: "half",
  } as RequestInit);
  await expect(readRequestBody(request)).rejects.toMatchObject({ status: 413 });
  expect(cancelled).toBe(true);
});

it("cancels a stalled body reader when the caller disconnects", async () => {
  const controller = new AbortController();
  let cancelled = false;
  const request = new Request("http://web.test", { method: "POST", signal: controller.signal,
    body: new ReadableStream({ cancel() { cancelled = true; } }), duplex: "half",
  } as RequestInit);
  const result = readRequestBody(request);
  const assertion = expect(result).rejects.toMatchObject({ name: "AbortError" });
  controller.abort();
  await assertion;
  expect(cancelled).toBe(true);
});
