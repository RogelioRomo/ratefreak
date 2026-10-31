import "server-only";

export function selectCookies(header: string | null, names: readonly string[]): string {
  const selected = new Map<string, string>();
  for (const entry of header?.split(";") ?? []) {
    const cookie = entry.trim();
    const separator = cookie.indexOf("=");
    const name = cookie.slice(0, separator);
    if (separator < 1 || !names.includes(name)) continue;
    if (selected.has(name)) throw new Error("Ambiguous cookies.");
    selected.set(name, cookie);
  }
  return [...selected.values()].join("; ");
}
