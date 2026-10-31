import "server-only";

export function isAllowedRoute(method: string, path: readonly string[]): boolean {
  return method === "GET" && path.length === 2 && (
    (path[0] === "auth" && path[1] === "csrf") ||
    (path[0] === "users" && path[1] === "me")
  );
}
