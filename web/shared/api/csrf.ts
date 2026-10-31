import type { CsrfResponse } from "@/shared/interfaces/csrf-response";
import type { PendingCsrf } from "@/shared/interfaces/pending-csrf";
import { ApiError } from "./api-error";
import { parseApiError } from "./parse-api-error";

let pending: PendingCsrf | undefined;

async function issueToken(signal: AbortSignal): Promise<string> {
  const response = await fetch("/api/v1/auth/csrf", {
    method: "GET", credentials: "same-origin", cache: "no-store", signal,
    headers: { Accept: "application/json" },
  });
  if (!response.ok) {
    const error = await parseApiError(response);
    signal.throwIfAborted();
    throw error;
  }
  let data: Partial<CsrfResponse> | null;
  try { data = await response.json(); }
  catch (error) {
    signal.throwIfAborted();
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    throw new ApiError(502, { title: "Invalid API response." });
  }
  if (!data || typeof data.requestToken !== "string" || !data.requestToken.trim()) {
    throw new ApiError(502, { title: "Invalid API response." });
  }
  signal.throwIfAborted();
  return data.requestToken;
}

// Each waiter can cancel without cancelling another mutation's token issuance.
function waitForToken(promise: Promise<string>, signals: AbortSignal[]): Promise<string> {
  return new Promise((resolve, reject) => {
    const cleanup = () => signals.forEach((signal) => signal.removeEventListener("abort", abort));
    const abort = () => {
      cleanup();
      reject(signals.find((signal) => signal.aborted)?.reason);
    };
    promise.then((token) => {
      cleanup();
      const cancelled = signals.find((signal) => signal.aborted);
      if (cancelled) reject(cancelled.reason);
      else resolve(token);
    }, (error: unknown) => { cleanup(); reject(error); });
    if (signals.some((signal) => signal.aborted)) abort();
    else signals.forEach((signal) => signal.addEventListener("abort", abort, { once: true }));
  });
}

export function getCsrfToken(signal?: AbortSignal): Promise<string> {
  signal?.throwIfAborted();
  if (!pending) {
    const controller = new AbortController();
    const promise = issueToken(controller.signal).finally(() => {
      if (pending?.controller === controller) pending = undefined;
    });
    pending = { controller, promise };
  }
  return waitForToken(pending.promise, signal ? [pending.controller.signal, signal] : [pending.controller.signal]);
}

export function resetCsrfState(): void {
  pending?.controller.abort();
  pending = undefined;
}
