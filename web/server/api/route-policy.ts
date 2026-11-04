import "server-only";
import { commentRoute } from "./comment-policy";
import { followRoute } from "./follow-policy";
import { collectionRoute } from "./collection-policy";
import { personalRoute } from "./personal-policy";
import { providerIdentity } from "./provider-policy";

export function isAllowedRoute(method: string, path: readonly string[]): boolean {
  if (path[0] === "users" && path.length === 2 && path[1] === "me" && method === "PATCH") return true;
  if (method === "GET" && path.length === 3 && path[0] === "users" && path[1] === "by-handle" && /^[a-z0-9_]{3,30}$/i.test(path[2])) return true;
  if (commentRoute(method, path) || followRoute(method, path) || collectionRoute(method, path) || personalRoute(method, path)) return true;
  if (path[0] === "media") {
    if (method === "POST" && path.length === 2 && path[1] === "import") return true;
    if (method === "GET" && path.length === 2 && path[1] === "providers") return true;
    if (method === "GET" && path[1] === "external") return (path.length === 3 && path[2] === "search") || (path.length === 5 && providerIdentity(path[2], path[3], path[4]) !== null);
  }
  if (method === "POST") return path.length === 2 && path[0] === "auth" &&
    ["register", "login", "logout"].includes(path[1]);
  if (method === "GET" && path[0] === "media") return path.length === 1 || (path.length === 2 && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(path[1]));
  return method === "GET" && path.length === 2 && (
    (path[0] === "auth" && path[1] === "csrf") ||
    (path[0] === "users" && path[1] === "me")
  );
}
