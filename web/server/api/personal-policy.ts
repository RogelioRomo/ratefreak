import "server-only";
import { isMediaId } from "@/features/catalog/query/filters";
export function personalRoute(method: string, path: readonly string[]): boolean {
  if (path[0] === "users" && path[1] === "me" && path[2] === "library") return (path.length === 3 && method === "GET") || (path.length === 4 && isMediaId(path[3]) && ["GET", "PUT", "DELETE"].includes(method));
  if (path.length === 3 && path[0] === "media" && isMediaId(path[1])) {
    if (path[2] === "rating") return ["GET", "PUT", "DELETE"].includes(method);
    if (path[2] === "review" || path[2] === "ratings") return method === "GET";
    if (path[2] === "reviews") return ["GET", "POST"].includes(method);
  }
  return path.length === 2 && path[0] === "reviews" && isMediaId(path[1]) && ["PATCH", "DELETE"].includes(method);
}
export function personalList(path: readonly string[]): "library" | "reviews" | null {
  if (path.length === 3 && path.join("/") === "users/me/library") return "library";
  if (path.length === 3 && path[0] === "media" && isMediaId(path[1]) && path[2] === "reviews") return "reviews";
  return null;
}
export function validatePersonalQuery(params: URLSearchParams, kind: "library" | "reviews"): URLSearchParams {
  const result = new URLSearchParams();
  for (const key of params.keys()) if (!(kind === "library" ? ["status", "type", "page", "pageSize"] : ["page", "pageSize"]).includes(key) || params.getAll(key).length !== 1) throw new Error("Invalid personal query.");
  for (const key of ["status", "type", "page", "pageSize"]) {
    const value = params.get(key); if (value === null) continue;
    if (key === "status" ? !["planned", "in_progress", "completed", "paused", "dropped"].includes(value) : key === "type" ? !["movie", "tv_show", "album", "video_game"].includes(value) : !/^\d+$/.test(value) || +value < 1 || +value > (key === "page" ? 10000 : 100)) throw new Error("Invalid personal filters.");
    result.set(key, key === "page" || key === "pageSize" ? String(+value) : value);
  }
  return result;
}
