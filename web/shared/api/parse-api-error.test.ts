import { expect, it } from "vitest";
import { parseApiError } from "./parse-api-error";

it("retains structured validation details and retry instructions", async () => {
  const error = await parseApiError(Response.json({ title: "Validation failed.", errors: { email: ["Invalid."] }, traceId: "t1" }, { status: 429, headers: { "Retry-After": "60" } }));
  expect(error).toMatchObject({ status: 429, message: "Validation failed.", retryAfter: "60", problem: { errors: { email: ["Invalid."] }, traceId: "t1" } });
});

it.each([401, 403, 429, 500])("handles empty, malformed and non-JSON errors for status %s", async (status) => {
  for (const response of [new Response("<html>secret</html>", { status }), new Response("", { status }), new Response("bad json", { status, headers: { "Content-Type": "application/problem+json" } }), Response.json({ title: { bad: "shape" }, errors: { a: "invalid" } }, { status })]) {
    const error = await parseApiError(response);
    expect(error.status).toBe(status);
    expect(error.message).toBe("Request failed.");
    expect(error.problem?.errors).toBeUndefined();
  }
});

it("preserves cancellation while reading an error body", async () => {
  const response = new Response(new ReadableStream({
    start(controller) { controller.error(new DOMException("Cancelled", "AbortError")); },
  }), { status: 503, headers: { "Content-Type": "application/problem+json" } });
  await expect(parseApiError(response)).rejects.toMatchObject({ name: "AbortError" });
});
