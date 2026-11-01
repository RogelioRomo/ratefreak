import "server-only";
import { validProviderType } from "@/features/providers/query/identity";
export { providerIdentity } from "@/features/providers/query/identity";
export function validateProviderSearch(params: URLSearchParams): URLSearchParams {
  for (const key of params.keys()) if (!["q", "provider", "type", "page"].includes(key) || params.getAll(key).length !== 1) throw new Error("Invalid provider query.");
  const raw = params.get("q") ?? "", provider = params.get("provider") ?? "tmdb", type = params.get("type") ?? "", page = params.get("page") ?? "1";
  if (!raw.trim() || raw.trim().length > 200 || /[\u0000-\u001f\u007f-\u009f]/.test(raw) || !validProviderType(provider, type) || !/^\d+$/.test(page) || +page < 1 || +page > 500) throw new Error("Invalid provider search.");
  return new URLSearchParams({ q: raw.trim(), provider, type, page: String(+page) });
}
