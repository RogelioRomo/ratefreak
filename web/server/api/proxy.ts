import "server-only";
import { moderationQuery } from "./moderation-policy";
import { commentList } from "./comment-policy";
import { followList, followQuery } from "./follow-policy";
import { collectionQuery } from "./collection-policy";
import { personalList, validatePersonalQuery } from "./personal-policy";
import { providerIdentity, validateProviderSearch } from "./provider-policy";
import { validateCatalogQuery } from "./catalog-query";
import { readApiProxyConfig } from "./config";
import { selectCookies } from "./cookies";
import { isAllowedRoute } from "./route-policy";
import { clientAttributionHeaders } from "./client-attribution";
import { validateMutation } from "./mutation-guard";
import { readRequestBody } from "./request-body";
import { ApiError } from "@/shared/api/api-error";

export function proxyProblem(status: number, title: string): Response {
  return Response.json({ status, title }, {
    status,
    headers: { "Content-Type": "application/problem+json", "Cache-Control": "no-store" },
  });
}

export async function proxyRequest(request: Request, path: readonly string[]): Promise<Response> {
  if (!["GET", "POST", "PUT", "PATCH", "DELETE"].includes(request.method)) return proxyProblem(405, "Method not allowed.");
  const incoming = new URL(request.url);
  const listKind = personalList(path);
  const socialList = request.method === "GET" && (followList(path) || commentList(path));
  let collectionParams: URLSearchParams | null = null;
  try { collectionParams = collectionQuery(request.method,path,incoming.searchParams); }
  catch { return proxyProblem(400,"Invalid collection filters."); }
  let moderationParams: URLSearchParams | null = null;
  try { moderationParams = moderationQuery(request.method,path,incoming.searchParams); }
  catch { return proxyProblem(400,"Invalid moderation filters."); }
  const externalSearch = path.length === 3 && path[0] === "media" && path[1] === "external" && path[2] === "search";
  if ((incoming.search && !((moderationParams !== null || collectionParams !== null || (request.method === "GET" && ((path.length === 1 && path[0] === "media") || externalSearch || listKind || socialList))))) || incoming.pathname !== `/api/v1/${path.join("/")}`) {
    return proxyProblem(404, "API route not found.");
  }
  if (!isAllowedRoute(request.method, path)) {
    return ["GET", "POST", "PUT", "PATCH", "DELETE"].some(method => isAllowedRoute(method, path))
      ? proxyProblem(405, "Method not allowed.") : proxyProblem(404, "API route not found.");
  }

  let query = "";
  if (socialList) {
    try { const value = followQuery(incoming.searchParams).toString(); query = value ? `?${value}` : ""; }
    catch { return proxyProblem(400, "Invalid connection paging."); }
  }
  if (path.length === 1 && path[0] === "media") {
    try { const validated = validateCatalogQuery(incoming.searchParams).toString(); query = validated ? `?${validated}` : ""; }
    catch { return proxyProblem(400, "Invalid catalog filters."); }
  }

  if (externalSearch) {
    try { query = `?${validateProviderSearch(incoming.searchParams)}`; }
    catch { return proxyProblem(400, "Invalid provider filters."); }
  }
  if (listKind && request.method === "GET") {
    try { const value = validatePersonalQuery(incoming.searchParams, listKind).toString(); query = value ? `?${value}` : ""; }
    catch { return proxyProblem(400, "Invalid personal filters."); }
  }
  if (collectionParams !== null) {const value=collectionParams.toString();query=value?`?${value}`:"";}
  if (moderationParams !== null) query = `?${moderationParams}`;
  let upstreamPath = incoming.pathname;
  if (path.length === 5 && path[0] === "media" && path[1] === "external") {
    const identity = providerIdentity(path[2], path[3], path[4])!;
    upstreamPath = `/api/v1/media/external/${identity.provider}/${identity.type}/${identity.externalId}`;
  }

  let config;
  try { config = readApiProxyConfig(); }
  catch { return proxyProblem(503, "API proxy is not configured."); }

  const headers = new Headers({ Accept: "application/json" });
  const mutation = request.method !== "GET";
  try {
    if (mutation) validateMutation(request, config);
    clientAttributionHeaders(request, config).forEach((value, name) => headers.set(name, value));
  } catch (error) {
    return error instanceof ApiError ? proxyProblem(error.status, error.message) : proxyProblem(503, "Client attribution is unavailable.");
  }
  try {
    const cookies = selectCookies(request.headers.get("cookie"), config.cookieNames);
    if (cookies) headers.set("Cookie", cookies);
  } catch { return proxyProblem(400, "Invalid session cookies."); }

  request.signal.throwIfAborted();
  const controller = new AbortController();
  const cancel = () => controller.abort(request.signal.reason);
  request.signal.addEventListener("abort", cancel, { once: true });
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort(new DOMException("Request timed out.", "TimeoutError"));
  }, config.timeoutMs);

  try {
    const requestBody = mutation ? await readRequestBody(request, controller.signal) : undefined;
    if (mutation) {
      headers.set("Content-Type", "application/json");
      headers.set("X-CSRF-TOKEN", request.headers.get("X-CSRF-TOKEN")!);
    }
    const upstream = await fetch(`${config.apiOrigin}${upstreamPath}${query}`, {
      method: request.method, headers, body: requestBody, cache: "no-store", redirect: "manual", signal: controller.signal,
    });
    if (upstream.status >= 300 && upstream.status < 400) {
      await upstream.body?.cancel();
      return proxyProblem(502, "Unexpected API redirect.");
    }
    const responseHeaders = new Headers({ "Cache-Control": "no-store" });
    for (const name of ["Content-Type", "Retry-After"]) {
      const value = upstream.headers.get(name);
      if (value) responseHeaders.set(name, value);
    }
    for (const cookie of upstream.headers.getSetCookie()) responseHeaders.append("Set-Cookie", cookie);
    // Read within the timeout so slow response bodies cannot outlive the budget.
    const body = upstream.status === 204 ? null : await upstream.arrayBuffer();
    controller.signal.throwIfAborted();
    return new Response(body, { status: upstream.status, headers: responseHeaders });
  } catch (error) {
    request.signal.throwIfAborted();
    if (error instanceof ApiError) return proxyProblem(error.status, "Request body is too large.");
    return timedOut ? proxyProblem(504, "The API timed out.") : proxyProblem(502, "The API is unavailable.");
  } finally {
    clearTimeout(timer);
    request.signal.removeEventListener("abort", cancel);
  }
}
