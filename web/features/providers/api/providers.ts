import { apiRequest } from "@/shared/api/request";
import type { ProviderCapability, ProviderFilters, ProviderIdentity, ExternalMedia, ExternalSearchPage, ImportMediaRequest, ImportMediaResult } from "../interfaces/provider";
import { discoveryParams } from "../query/filters";
import { providerIdentity } from "../query/identity";
export const getProviders = (signal?: AbortSignal) => apiRequest<ProviderCapability[]>("media/providers", { signal });
export const getExternalSearch = (filters: ProviderFilters, signal?: AbortSignal) => apiRequest<ExternalSearchPage>(`media/external/search?${discoveryParams(filters)}`, { signal });
export function getExternalMedia(identity: ProviderIdentity, signal?: AbortSignal): Promise<ExternalMedia> {
  const normalized = providerIdentity(identity.provider, identity.type, identity.externalId);
  if (!normalized) return Promise.reject(new Error("Invalid provider identity."));
  return apiRequest(`media/external/${normalized.provider}/${normalized.type}/${normalized.externalId}`, { signal });
}
export const importMedia = (input: ImportMediaRequest, signal?: AbortSignal) => apiRequest<ImportMediaResult>("media/import", { method: "POST", body: input, signal });
