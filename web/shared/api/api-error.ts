import type { ProblemDetails } from "@/shared/interfaces/problem-details";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem?: ProblemDetails,
    public readonly retryAfter?: string,
  ) {
    super(problem?.title || "Request failed.");
    this.name = "ApiError";
  }
}
