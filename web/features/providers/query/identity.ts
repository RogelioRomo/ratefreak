import type { ProviderIdentity, ProviderName } from "../interfaces/provider";
export const providerNames: Record<ProviderName, string> = { tmdb: "TMDB", musicbrainz: "MusicBrainz", igdb: "IGDB" };
export const providerHomes: Record<ProviderName, string> = { tmdb: "https://www.themoviedb.org", musicbrainz: "https://musicbrainz.org", igdb: "https://www.igdb.com" };
export const providerTypes = { tmdb: ["movie", "tv_show"], musicbrainz: ["album"], igdb: ["video_game"] } as const;
export function validProviderType(provider: string, type: string): boolean {
  return Object.hasOwn(providerTypes, provider) && (providerTypes[provider as ProviderName] as readonly string[]).includes(type);
}
export function providerIdentity(provider: string, type: string, id: string): ProviderIdentity | null {
  if (!validProviderType(provider, type)) return null;
  let externalId: string;
  if (provider === "musicbrainz") {
    if (!/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i.test(id) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(id)) return null;
    externalId = id.toLowerCase();
  } else {
    if (!/^\d{1,10}$/.test(id) || +id < 1 || +id > 2147483647) return null;
    externalId = String(+id);
  }
  return { provider, type, externalId } as ProviderIdentity;
}
export function previewHref(identity: ProviderIdentity): string {
  return `/catalog/discover/${identity.provider}/${identity.type}/${identity.externalId}`;
}
