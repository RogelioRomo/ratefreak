import type { ProblemDetails } from "@/shared/interfaces/problem-details";
import { ApiError } from "./api-error";

export async function parseApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | undefined;
  const contentType = response.headers.get("content-type")?.split(";", 1)[0].trim();
  if (contentType === "application/json" || contentType === "application/problem+json") {
    try {
      const value: unknown = await response.json();
      if (value && typeof value === "object" && !Array.isArray(value)) {
        const data = value as Record<string, unknown>;
        problem = {};
        for (const key of ["type", "title", "detail", "instance", "traceId"] as const) {
          if (typeof data[key] === "string") problem[key] = data[key];
        }
        if (typeof data.status === "number") problem.status = data.status;
        if (data.errors && typeof data.errors === "object" && !Array.isArray(data.errors)) {
          const entries = Object.entries(data.errors);
          if (entries.every(([, messages]) => Array.isArray(messages) && messages.every((message) => typeof message === "string"))) {
            problem.errors = Object.fromEntries(entries) as Record<string, string[]>;
          }
        }
      }
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") throw error;
      // Gateways may send HTML, empty bodies or malformed JSON. Never surface it.
    }
  }
  return new ApiError(response.status, problem, response.headers.get("retry-after") ?? undefined);
}
