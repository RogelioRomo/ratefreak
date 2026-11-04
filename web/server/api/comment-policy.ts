import "server-only";
import { isMediaId } from "@/features/catalog/query/filters";
export function commentList(path: readonly string[]): boolean { return path.length === 3 && path[0] === "reviews" && isMediaId(path[1]) && path[2] === "comments"; }
export function commentRoute(method: string, path: readonly string[]): boolean {
 if (commentList(path)) return ["GET", "POST"].includes(method);
 if (path.length !== 2 || !isMediaId(path[1])) return false;
 return path[0] === "comments" && ["GET", "PATCH", "DELETE"].includes(method) || path[0] === "reviews" && method === "GET";
}
