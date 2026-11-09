import "server-only";
import { ApiError } from "@/shared/api/api-error";
import { readApiProxyConfig } from "./config";
import { clientAttributionHeaders } from "./client-attribution";
import { validateMutation } from "./mutation-guard";
import { readRequestBody } from "./request-body";
import { applyOAuthCookies, oauthCookieHeader } from "./oauth-cookies";
import { authorizationUrl, callbackQuery, oauthRoute } from "./oauth-policy";

function problem(status: number, title: string): Response {
  return Response.json({ status, title }, { status, headers: { "Cache-Control": "no-store", "Referrer-Policy": "no-referrer" } });
}

export async function proxyOAuthRequest(request: Request, path: readonly string[]): Promise<Response> {
  const route = oauthRoute(request.method, path)!;
  const incoming = new URL(request.url);
  if (incoming.pathname !== `/api/v1/${path.join("/")}` || (route.kind === "start" && incoming.search)) return problem(404, "API route not found.");
  let config;
  try { config = readApiProxyConfig(); if (!config.webOrigin || !config.proxySecret || !config.clientIpMode) throw new Error(); }
  catch { return problem(503, "External sign-in is not configured."); }
  const origin = config.webOrigin!;
  const development = process.env.NODE_ENV === "development";
  const headers = new Headers({ Accept: "application/json", "X-RateFreak-Browser-Origin": origin });
  let cookies: string; let query = "";
  try {
    if (route.kind === "start") validateMutation(request, config);
    else query = callbackQuery(incoming);
    clientAttributionHeaders(request, config).forEach((v, k) => headers.set(k, v));
    cookies = oauthCookieHeader(request.headers.get("cookie"), development);
    if (cookies) headers.set("Cookie", cookies);
  } catch (error) { return error instanceof ApiError ? problem(error.status, error.message) : problem(400, "Invalid external sign-in request."); }

  request.signal.throwIfAborted();
  const controller = new AbortController();
  const cancel = () => controller.abort(request.signal.reason);
  request.signal.addEventListener("abort", cancel, { once: true });
  let timedOut = false;
  const timer = setTimeout(() => { timedOut = true; controller.abort(new DOMException("Request timed out.", "TimeoutError")); }, config.timeoutMs);
  const issued: string[] = [];
  let retryAfter: string | null = null;
  const finish = (response: Response) => {
    response.headers.set("Cache-Control", "no-store"); response.headers.set("Referrer-Policy", "no-referrer");
    if (retryAfter && /^\d{1,5}$/.test(retryAfter)) response.headers.set("Retry-After", retryAfter);
    for (const cookie of issued) response.headers.append("Set-Cookie", cookie);
    return response;
  };
  const navigate = (destination: string) => finish(new Response(null, { status: 303, headers: { Location: `${origin}${destination}` } }));
  const failure = (status = 502) => route.kind === "callback" ? navigate("/auth/external/error?reason=failed") : finish(problem(status, "External sign-in could not be started. Please try again."));
  const acceptCookies = (response: Response) => {
    const next = response.headers.getSetCookie();
    cookies = applyOAuthCookies(cookies, next, development);
    issued.push(...next);
    if (cookies) headers.set("Cookie", cookies); else headers.delete("Cookie");
  };
  try {
    const body = route.kind === "start" ? await readRequestBody(request, controller.signal) : undefined;
    if (route.kind === "start") { headers.set("Content-Type", "application/json"); headers.set("X-CSRF-TOKEN", request.headers.get("X-CSRF-TOKEN")!); }
    const response = await fetch(`${config.apiOrigin}/api/v1/${path.join("/")}${query ? `?${query}` : ""}`, {
      method: request.method, headers, body, redirect: "manual", cache: "no-store", signal: controller.signal,
    });
    retryAfter = response.headers.get("Retry-After");
    acceptCookies(response);
    if (route.kind === "start") {
      if (response.status !== 302) {
        await response.body?.cancel();
        return failure([400,401,403,409,429,503].includes(response.status) ? response.status : 502);
      }
      const url = authorizationUrl(response.headers.get("location") ?? "", route.provider, origin);
      await response.body?.cancel();
      return finish(Response.json({ authorizationUrl: url }));
    }
    if (response.status !== 302) { await response.body?.cancel(); return failure(); }
    const target = response.headers.get("location");
    // Only these literal forms can be a completion hop. Never normalize arbitrary URLs.
    if (!["/api/v1/auth/external/complete", `${origin}/api/v1/auth/external/complete`].includes(target ?? "")) { await response.body?.cancel(); return failure(); }
    await response.body?.cancel();
    const completed = await fetch(`${config.apiOrigin}/api/v1/auth/external/complete`, { headers, redirect: "manual", cache: "no-store", signal: controller.signal });
    acceptCookies(completed);
    if (!completed.ok) {
      await completed.body?.cancel();
      return completed.status === 409 ? navigate("/auth/external/error?reason=conflict") : failure();
    }
    // Read completion JSON within the same timeout and size budget as other bodies.
    const bounded = new Request("http://internal.invalid", { method: "POST", headers: completed.headers, body: completed.body, duplex: "half" } as RequestInit);
    const bytes = await readRequestBody(bounded, controller.signal);
    const data = JSON.parse(new TextDecoder().decode(bytes));
    controller.signal.throwIfAborted();
    if (!data || typeof data !== "object" || Array.isArray(data)) return failure();
    if (data.reauthenticated === true && data.provider === route.provider && ["export", "delete"].includes(data.operation)) return navigate(`/account?dataAction=${data.operation}`);
    if (data.requiresRegistration === true && data.provider === route.provider) return navigate("/auth/external/register");
    if (data.linked === true && data.provider === route.provider) return navigate("/account");
    if (typeof data.id === "string" && /^[0-9a-f]{8}-[0-9a-f-]{27}$/i.test(data.id) && typeof data.handle === "string" && typeof data.email === "string" && typeof data.displayName === "string" && typeof data.createdAt === "string") return navigate("/");
    return failure();
  } catch (error) {
    request.signal.throwIfAborted();
    if (error instanceof ApiError && route.kind === "start") return finish(problem(error.status, "Invalid external sign-in request."));
    return failure(timedOut ? 504 : 502);
  } finally {
    clearTimeout(timer); request.signal.removeEventListener("abort", cancel);
  }
}
