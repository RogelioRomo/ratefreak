import { getCsrfToken } from "@/shared/api/csrf";
import { parseApiError } from "@/shared/api/parse-api-error";
import { ApiError } from "@/shared/api/api-error";
import type { AvatarState } from "../interfaces/avatar";

async function changeAvatar(body: FormData | string, method: "PUT" | "DELETE", expectedAccountId: string, signal: AbortSignal): Promise<AvatarState> {
  const token = await getCsrfToken(signal); signal.throwIfAborted();
  const headers: Record<string, string> = { Accept: "application/json", "X-CSRF-TOKEN": token, "X-RateFreak-Account-Id": expectedAccountId };
  if (typeof body === "string") headers["Content-Type"] = "application/json";
  const response = await fetch("/api/v1/users/me/avatar", { method, headers, body, credentials: "same-origin", cache: "no-store", signal });
  if (!response.ok) throw await parseApiError(response);
  signal.throwIfAborted();
  try {
    const value = await response.json() as AvatarState;
    if (!Number.isSafeInteger(value.revision) || value.revision < 0 || (value.avatarUrl !== null && (typeof value.avatarUrl !== "string" || !/^\/api\/v1\/users\/me\/avatar\/[0-9a-f-]{36}$/i.test(value.avatarUrl)))) throw new Error("Invalid avatar response");
    signal.throwIfAborted(); return value;
  } catch (error) { signal.throwIfAborted(); if (error instanceof DOMException && error.name === "AbortError") throw error; throw new ApiError(502, { title: "Invalid avatar response." }); }
}
export function uploadAvatar(file: File, expectedRevision: number, expectedAccountId: string, signal: AbortSignal): Promise<AvatarState> {
  const body = new FormData(); body.set("expectedRevision", String(expectedRevision)); body.set("file", file);
  return changeAvatar(body, "PUT", expectedAccountId, signal);
}
export function removeAvatar(expectedRevision: number, expectedAccountId: string, signal: AbortSignal): Promise<AvatarState> {
  return changeAvatar(JSON.stringify({ expectedRevision }), "DELETE", expectedAccountId, signal);
}
