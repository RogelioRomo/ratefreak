import "server-only";
import { isMediaId } from "@/features/catalog/query/filters";
export const isAvatarUpload = (method: string, path: readonly string[]) => method === "PUT" && path.length === 3 && path[0] === "users" && path[1] === "me" && path[2] === "avatar";
export function avatarRoute(method: string, path: readonly string[]): boolean {
  if (path[0] === "users" && path[2] === "avatar") {
    if (path.length === 3 && path[1] === "me") return method === "PUT" || method === "DELETE";
    return method === "GET" && path.length === 4 && (path[1] === "me" || isMediaId(path[1])) && isMediaId(path[3]);
  }
  return method === "GET" && path.length === 5 && path[0] === "moderation" && ["reports", "profiles"].includes(path[1]) && isMediaId(path[2]) && path[3] === "avatar" && isMediaId(path[4]);
}
