import "server-only";

export function isAllowedRoute(method: string, path: readonly string[]): boolean {
  if (method === "POST") return path.length === 2 && path[0] === "auth" &&
    ["register", "login", "logout"].includes(path[1]);
  return method === "GET" && path.length === 2 && (
    (path[0] === "auth" && path[1] === "csrf") ||
    (path[0] === "users" && path[1] === "me")
  );
}
