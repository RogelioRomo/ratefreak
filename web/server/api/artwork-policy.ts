import "server-only";
import { isMediaId } from "@/features/catalog/query/filters";
export const isArtworkUpload = (method: string, path: readonly string[]) => method === "PUT" && path.length === 3 && path[0] === "media" && isMediaId(path[1]) && path[2] === "artwork";
export function customCatalogRoute(method: string, path: readonly string[]): boolean {
  if (method === "GET" && path.length === 5 && path[0] === "moderation" && ["reports", "media"].includes(path[1]) && isMediaId(path[2]) && path[3] === "artwork" && isMediaId(path[4])) return true;
  if (path[0] === "categories") return path.length === 1 && ["GET", "POST"].includes(method) || path.length === 2 && isMediaId(path[1]) && method === "PATCH";
  if (path[0] === "media") {
    if (path.length === 1) return method === "POST";
    if (!isMediaId(path[1] ?? "")) return false;
    if (path.length === 2) return method === "PATCH";
    if (path.length === 3) return path[2] === "artwork" && ["PUT", "DELETE"].includes(method) || path[2] === "manage" && method === "GET" || ["publish", "unpublish", "archive", "restore"].includes(path[2]) && method === "POST";
    return path.length === 4 && path[2] === "artwork" && isMediaId(path[3]) && method === "GET";
  }
  if (path[0] === "moderation" && path[1] === "categories" && path.length === 2 && method === "GET") return true;
  return path[0] === "moderation" && path[1] === "categories" && path.length === 4 && isMediaId(path[2]) && ["rename", "merge", "hide", "restore"].includes(path[3]) && method === "POST";
}
export function categoryQuery(method: string, path: readonly string[], params: URLSearchParams): URLSearchParams | null {
  if (method !== "GET" || !(path.length === 1 && path[0] === "categories" || path.length === 2 && path[0] === "moderation" && path[1] === "categories")) return null;
  const result = new URLSearchParams();
  for (const [key, value] of params) {
    if (params.getAll(key).length !== 1 || !["q", "ownOnly", "page", "pageSize"].includes(key)) throw new Error("Invalid category query.");
    if (key === "q" && (value.length > 100 || value.includes("\0")) || key === "ownOnly" && !["true", "false"].includes(value) || ["page", "pageSize"].includes(key) && (!/^\d+$/.test(value) || Number(value) < 1 || Number(value) > (key === "page" ? 10000 : 100))) throw new Error("Invalid category query.");
    result.set(key, value);
  }
  return result;
}
