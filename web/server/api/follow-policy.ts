import "server-only";
import { isMediaId } from "@/features/catalog/query/filters";
import { validatePersonalQuery } from "./personal-policy";
export function followList(path: readonly string[]): boolean { return path.length === 3 && path[0] === "users" && isMediaId(path[1]) && ["followers", "following"].includes(path[2]); }
export function followRoute(method: string, path: readonly string[]): boolean {
 return method === "GET" && followList(path) || path.length === 3 && path[0] === "users" && isMediaId(path[1]) && path[2] === "follow" && ["PUT", "DELETE"].includes(method);
}
export const followQuery = (params: URLSearchParams) => validatePersonalQuery(params, "reviews");
