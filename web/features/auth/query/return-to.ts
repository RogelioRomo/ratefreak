import { isMediaId } from "@/features/catalog/query/filters";
import { previewHref, providerIdentity } from "@/features/providers/query/identity";
export function safePreviewReturnTo(value: string | null): string | null {
  if (!value) return null;
  if (value === "/collections" || value === "/collections/new") return value;
  if (value.startsWith("/collections/") && isMediaId(value.slice(13)) && value === value.toLowerCase()) return value;
  if (value === "/library") return value;
  if (value.startsWith("/catalog/") && isMediaId(value.slice(9)) && value === value.toLowerCase()) return value;
  const parts = value.split("/");
  if (parts.length !== 6 || parts[0] !== "" || parts[1] !== "catalog" || parts[2] !== "discover") return null;
  const identity = providerIdentity(parts[3], parts[4], parts[5]);
  return identity && previewHref(identity) === value ? value : null;
}
