import "server-only";
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
  if (!["GET", "POST"].includes(request.method)) return proxyProblem(405, "Method not allowed.");
  const incoming = new URL(request.url);
  if (incoming.search || incoming.pathname !== `/api/v1/${path.join("/")}`) {
    return proxyProblem(404, "API route not found.");
  }
  if (!isAllowedRoute(request.method, path)) {
    return isAllowedRoute("GET", path) || isAllowedRoute("POST", path)
      ? proxyProblem(405, "Method not allowed.") : proxyProblem(404, "API route not found.");
  }

  let config;
  try { config = readApiProxyConfig(); }
  catch { return proxyProblem(503, "API proxy is not configured."); }

  const headers = new Headers({ Accept: "application/json" });
  const mutation = request.method === "POST";
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
    const upstream = await fetch(`${config.apiOrigin}${incoming.pathname}`, {
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
