import "server-only";

function allowed(name: string, development: boolean): boolean {
  const prefix = development ? "" : "__Host-";
  if ([`${prefix}ratefreak.session`, `${prefix}ratefreak.csrf`, `${prefix}ratefreak.external`].includes(name)) return true;
  if (new RegExp(`^${prefix}ratefreak\\.externalC[1-5]$`).test(name)) return true;
  // The framework appends a random base64url correlation ID to this scheme prefix.
  return /^\.AspNetCore\.Correlation\.(google|github)\.[A-Za-z0-9_-]{1,128}$/.test(name);
}

export function oauthCookieHeader(header: string | null, development: boolean): string {
  const values = new Map<string, string>();
  let size = 0;
  for (const part of header?.split(";") ?? []) {
    const cookie = part.trim(); const index = cookie.indexOf("=");
    if (index < 1) continue;
    const name = cookie.slice(0, index);
    const external = `${development ? "" : "__Host-"}ratefreak.external`;
    if (name.startsWith(`${external}C`) && /^C[0-9]+$/.test(name.slice(external.length)) && !/^C[1-5]$/.test(name.slice(external.length))) throw new Error("Excessive ticket chunks.");
    if (name === external && cookie.slice(index + 1).startsWith("chunks-") && !/^chunks-[1-5]$/.test(cookie.slice(index + 1))) throw new Error("Excessive ticket chunks.");
    if (!allowed(name, development)) continue;
    if (values.has(name) || /[\r\n\x00-\x1f\x7f]/.test(cookie)) throw new Error("Invalid OAuth cookies.");
    size += cookie.length;
    if (size > 16384 || values.size >= 32) throw new Error("Excessive OAuth cookies.");
    values.set(name, cookie);
  }
  return [...values.values()].join("; ");
}

export function applyOAuthCookies(header: string, issued: readonly string[], development: boolean): string {
  const values = new Map(header.split("; ").filter(Boolean).map(c => [c.slice(0, c.indexOf("=")), c]));
  for (const cookie of issued) {
    const pair = cookie.split(";", 1)[0]; const index = pair.indexOf("=");
    if (index < 1) throw new Error("Invalid upstream cookie.");
    const name = pair.slice(0, index);
    if (!allowed(name, development)) throw new Error("Unexpected upstream cookie.");
    if (pair.slice(index + 1) === "" || /;\s*max-age=0(?:;|$)/i.test(cookie)) values.delete(name);
    else values.set(name, pair);
  }
  return oauthCookieHeader([...values.values()].join("; "), development);
}
