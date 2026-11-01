import type { ProviderCapability, ProviderFilters, ProviderName } from "../interfaces/provider";
import type { DiscoveryState } from "../interfaces/discovery";
import { providerTypes, validProviderType } from "./identity";
export function availableProviders(capabilities: ProviderCapability[]): ProviderCapability[] {
  return ["tmdb", "musicbrainz", "igdb"].flatMap(name => capabilities.filter(cap => cap.name === name && cap.enabled && cap.supportedTypes.some(type => validProviderType(name, type))));
}
export function parseDiscovery(params: URLSearchParams, capabilities: ProviderCapability[]): DiscoveryState {
  const enabled = availableProviders(capabilities);
  const invalid = (error: string): DiscoveryState => ({ filters: null, submitted: false, error });
  if (!enabled.length) return invalid("No metadata providers are currently available.");
  for (const key of params.keys()) if (!["q", "provider", "type", "page"].includes(key) || params.getAll(key).length !== 1) return invalid("Invalid discovery filters.");
  const provider = params.get("provider") ?? enabled[0].name;
  const cap = enabled.find(x => x.name === provider);
  if (!cap) return invalid("This provider is currently unavailable.");
  const type = params.get("type") ?? providerTypes[provider as ProviderName].find(t => cap.supportedTypes.includes(t))!;
  const raw = params.get("q") ?? "", page = params.get("page") ?? "1";
  if (!validProviderType(provider, type) || !cap.supportedTypes.includes(type as ProviderFilters["type"]) || /[\u0000-\u001f\u007f-\u009f]/.test(raw) || raw.trim().length > 200 || (params.has("q") && !raw.trim()) || !/^\d+$/.test(page) || +page < 1 || +page > 500) return invalid("Invalid discovery filters.");
  return { filters: { q: raw.trim(), provider, type, page: +page } as ProviderFilters, submitted: params.has("q"), error: null };
}
export function discoveryParams(filters: ProviderFilters): URLSearchParams {
  return new URLSearchParams({ q: filters.q.trim(), provider: filters.provider, type: filters.type, page: String(filters.page) });
}
export function discoveryHref(filters: ProviderFilters): string { return `/catalog/discover?${discoveryParams(filters)}`; }
