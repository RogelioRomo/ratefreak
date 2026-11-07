import "server-only";

export function oauthProvider(value: string): value is "google" | "github" {
  return value === "google" || value === "github";
}

export function oauthRoute(method: string, path: readonly string[]) {
  if (path.length === 4 && path[0] === "auth" && path[1] === "external" && oauthProvider(path[2])) {
    if (method === "POST" && path[3] === "start") return { provider: path[2], kind: "start" as const };
    if (method === "GET" && path[3] === "callback") return { provider: path[2], kind: "callback" as const };
  }
  if (method === "POST" && path.length === 5 && path[0] === "users" && path[1] === "me" && path[2] === "logins" && oauthProvider(path[3]) && path[4] === "start") {
    return { provider: path[3], kind: "start" as const };
  }
  return null;
}

export function externalTicketRoute(path: readonly string[]): boolean {
  return path.length === 3 && path[0] === "auth" && path[1] === "external" && ["pending", "register"].includes(path[2]);
}

export function authorizationUrl(value: string, provider: string, origin: string): string {
  const url = new URL(value);
  const endpoint = provider === "google" ? "https://accounts.google.com/o/oauth2/v2/auth" : "https://github.com/login/oauth/authorize";
  if (`${url.origin}${url.pathname}` !== endpoint || url.username || url.password || url.hash) throw new Error("Invalid authorization URL.");
  for (const name of ["client_id", "redirect_uri", "state", "code_challenge", "code_challenge_method"]) {
    if (url.searchParams.getAll(name).length !== 1 || !url.searchParams.get(name)) throw new Error("Invalid authorization parameters.");
  }
  if (url.searchParams.get("redirect_uri") !== `${origin}/api/v1/auth/external/${provider}/callback` || url.searchParams.get("code_challenge_method") !== "S256") throw new Error("Invalid callback.");
  return value;
}

export function callbackQuery(url: URL): string {
  if (url.search.length > 8192) throw new Error("Excessive callback query.");
  const result = new URLSearchParams();
  for (const key of ["code", "state", "error", "error_description", "error_uri"]) {
    const values = url.searchParams.getAll(key);
    if (values.length > 1) throw new Error("Ambiguous callback query.");
    if (values.length) result.set(key, values[0]);
  }
  return result.toString();
}
