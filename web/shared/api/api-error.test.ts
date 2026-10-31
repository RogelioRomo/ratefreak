import { describe, expect, it } from "vitest";
import { ApiError } from "./api-error";

describe("ApiError", () => {
  it("preserves validation context and retry instructions", () => {
    const error = new ApiError(429, {
      title: "Too many requests.",
      errors: { email: ["Invalid email."] },
      traceId: "trace-1",
    }, "120");
    expect(error).toBeInstanceOf(Error);
    expect(error.status).toBe(429);
    expect(error.message).toBe("Too many requests.");
    expect(error.problem?.errors?.email).toEqual(["Invalid email."]);
    expect(error.problem?.traceId).toBe("trace-1");
    expect(error.retryAfter).toBe("120");
  });

  it("uses a safe fallback when no problem title exists", () => {
    expect(new ApiError(500).message).toBe("Request failed.");
  });
});
