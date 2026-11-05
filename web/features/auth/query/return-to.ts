import { isMediaId } from "@/features/catalog/query/filters";
import { previewHref, providerIdentity } from "@/features/providers/query/identity";
export function safePreviewReturnTo(value: string | null): string | null {
  if (!value) return null;
  const paged = /^\/(reviews|catalog)\/([0-9a-f-]+)\?(page|reviewsPage)=([1-9][0-9]{0,4})$/.exec(value);
  if (paged && isMediaId(paged[2]) && Number(paged[4]) <= 10000 && paged[3] === (paged[1] === 'reviews' ? 'page' : 'reviewsPage')) return value;
  if (value.startsWith("/reviews/") && isMediaId(value.slice(9)) && value === value.toLowerCase()) return value;
  if (/^\/users\/[a-z0-9_]{3,30}$/i.test(value)) return value;
  if (value === "/collections" || value === "/collections/new") return value;
  if (value.startsWith("/collections/") && isMediaId(value.slice(13)) && value === value.toLowerCase()) return value;
  if (value === "/library") return value;
  if (value.startsWith("/catalog/") && isMediaId(value.slice(9)) && value === value.toLowerCase()) return value;
  const parts = value.split("/");
  if (parts.length !== 6 || parts[0] !== "" || parts[1] !== "catalog" || parts[2] !== "discover") return null;
  const identity = providerIdentity(parts[3], parts[4], parts[5]);
  return identity && previewHref(identity) === value ? value : null;
}
