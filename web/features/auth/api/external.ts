import { apiRequest } from "@/shared/api/request";
import { ApiError } from "@/shared/api/api-error";
import type { ExternalProvider } from "../interfaces/external-provider";
import type { ProviderAvailability } from "../interfaces/provider-availability";
import type { LinkedProviders } from "../interfaces/linked-providers";
import type { PendingExternal } from "../interfaces/pending-external";
import type { AccountProfile } from "../interfaces/account-profile";
import type { ExternalRegistrationRequest } from "../interfaces/external-registration-request";
function provider(value: unknown): value is ExternalProvider { return value === "google" || value === "github"; }
function invalid(): never { throw new ApiError(502, { title: "Invalid sign-in response." }); }
export async function getProviders(signal?: AbortSignal): Promise<ProviderAvailability[]> {
  const data = await apiRequest<ProviderAvailability[]>("auth/providers", { signal });
  if (!Array.isArray(data) || data.length > 2 || data.some(item => !item || !provider(item.name) || typeof item.enabled !== "boolean") || new Set(data.map(item => item.name)).size !== data.length) invalid();
  return data;
}
export async function startExternal(name: ExternalProvider, link = false): Promise<string> {
  const data = await apiRequest<{ authorizationUrl: string }>(link ? `users/me/logins/${name}/start` : `auth/external/${name}/start`, { method: "POST", body: {} });
  if (!data || typeof data.authorizationUrl !== "string") invalid();
  const url = new URL(data.authorizationUrl);
  const endpoint = name === "google" ? "https://accounts.google.com/o/oauth2/v2/auth" : "https://github.com/login/oauth/authorize";
  if (`${url.origin}${url.pathname}` !== endpoint || url.username || url.password || url.hash) invalid();
  return data.authorizationUrl;
}
export async function getLinkedProviders(signal?: AbortSignal): Promise<LinkedProviders> {
  const data = await apiRequest<LinkedProviders>("users/me/logins", { signal });
  if (!data || typeof data.hasPassword !== "boolean" || !Array.isArray(data.providers) || data.providers.some(item => !provider(item)) || new Set(data.providers).size !== data.providers.length) invalid();
  return data;
}
export async function getPendingExternal(signal?: AbortSignal): Promise<PendingExternal | null> {
  try {
    const data = await apiRequest<PendingExternal>("auth/external/pending", { signal });
    if (!data || data.requiresRegistration !== true || !provider(data.provider)) invalid();
    return data;
  } catch (error) { if (error instanceof ApiError && error.status === 401) return null; throw error; }
}
export function registerExternal(input: ExternalRegistrationRequest): Promise<AccountProfile> {
  return apiRequest("auth/external/register", { method: "POST", body: input });
}
