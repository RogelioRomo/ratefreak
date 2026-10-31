import "server-only";
import { readApiProxyConfig } from "./config";
import { selectCookies } from "./cookies";
import { isAllowedRoute } from "./route-policy";

export function proxyProblem(status: number, title: string): Response {
  return Response.json({ status, title }, {
    status,
    headers: { "Content-Type": "application/problem+json", "Cache-Control": "no-store" },
  });
}

export async function proxyRequest(request: Request, path: readonly string[]): Promise<Response> {
  if (request.method !== "GET") return proxyProblem(405, "Method not allowed.");
  const incoming = new URL(request.url);
  if (!isAllowedRoute(request.method, path) || incoming.search || incoming.pathname !== `/api/v1/${path.join("/")}`) {
    return proxyProblem(404, "API route not found.");
  }

  let config;
  try { config = readApiProxyConfig(); }
  catch { return proxyProblem(503, "API proxy is not configured."); }

  const headers = new Headers({ Accept: "application/json" });
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
    const upstream = await fetch(`${config.apiOrigin}${incoming.pathname}`, {
      method: "GET", headers, cache: "no-store", redirect: "manual", signal: controller.signal,
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
  } catch {
    request.signal.throwIfAborted();
    return timedOut ? proxyProblem(504, "The API timed out.") : proxyProblem(502, "The API is unavailable.");
  } finally {
    clearTimeout(timer);
    request.signal.removeEventListener("abort", cancel);
  }
}
