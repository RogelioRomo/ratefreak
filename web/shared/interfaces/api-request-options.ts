export interface ApiRequestOptions<T = unknown> {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  signal?: AbortSignal;
  decode?: (text: string) => T;
  encodeBody?: (body: unknown) => string;
}
