import type { ApiRequestOptions } from "@/shared/interfaces/api-request-options";
import { ApiError } from "./api-error";
import { getCsrfToken } from "./csrf";
import { parseApiError } from "./parse-api-error";

function validatePath(path: string): void {
  const pathname = path.split("?", 1)[0];
  // Simple canonical segments prevent URL normalization from changing the route.
  if (!/^[A-Za-z0-9_-]+(?:\/[A-Za-z0-9_-]+)*$/.test(pathname) || /[#\s\\]/.test(path)) {
    throw new Error("Invalid API path.");
  }
}

export async function apiRequest<T>(path: string, options: ApiRequestOptions<T> = {}): Promise<T> {
  validatePath(path);
  options.signal?.throwIfAborted();
  const method = options.method ?? "GET";
  const headers: Record<string, string> = { Accept: "application/json" };
  if (method !== "GET") headers["X-CSRF-TOKEN"] = await getCsrfToken(options.signal);
  options.signal?.throwIfAborted();
  if (method !== "GET" || options.body !== undefined) headers["Content-Type"] = "application/json";
  const response = await fetch(`/api/v1/${path}`, {
    method, headers, credentials: "same-origin", cache: "no-store", signal: options.signal,
    body: options.body === undefined ? undefined : (options.encodeBody ?? JSON.stringify)(options.body),
  });
  if (!response.ok) {
    const error = await parseApiError(response);
    options.signal?.throwIfAborted();
    throw error;
  }
  options.signal?.throwIfAborted();
  if (response.status === 204) return undefined as T;
  try {
    const data = options.decode ? options.decode(await response.text()) : await response.json() as T;
    options.signal?.throwIfAborted();
    return data;
  }
  catch (error) {
    options.signal?.throwIfAborted();
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    throw new ApiError(502, { title: "Invalid API response." });
  }
}
