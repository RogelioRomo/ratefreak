import "server-only";
import { isMediaId } from "@/features/catalog/query/filters";
import { validatePersonalQuery } from "./personal-policy";
export function notificationList(method: string, path: readonly string[]): boolean {
  return method === "GET" && path.length === 1 && path[0] === "notifications";
}
export function notificationRoute(method: string, path: readonly string[]): boolean {
  if (path[0] !== "notifications") return false;
  if (path.length === 1) return method === "GET" || method === "DELETE";
  if (path.length === 2) return path[1] === "unread-count" && method === "GET" || path[1] === "read-all" && method === "POST" || isMediaId(path[1]) && method === "DELETE";
  return path.length === 3 && isMediaId(path[1]) && path[2] === "read" && ["PUT", "DELETE"].includes(method);
}
export const notificationQuery = (params: URLSearchParams) => validatePersonalQuery(params, "reviews");

export function validateNotificationBody(body: Uint8Array): void {
  if (!body.byteLength) return;
  const value: unknown = JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(body));
  if (!value || typeof value !== "object" || Array.isArray(value) || Object.keys(value).length) throw new Error("Notification commands require an empty object.");
}
