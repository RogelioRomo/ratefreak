import type { ProviderCapability, ProviderFilters, ProviderIdentity, ExternalMedia, ExternalSearchPage } from "./provider";
export interface DiscoveryFormProps { capabilities: ProviderCapability[]; filters: ProviderFilters; }
export interface ProviderAttributionProps { capability: ProviderCapability; }
export interface ProviderPreviewProps { provider: string; type: string; externalId: string; }
export interface ProviderPreviewPageProps { params: Promise<ProviderPreviewProps>; }
export interface ImportControlProps { identity: ProviderIdentity; }

export interface DiscoveryResultsProps { data: ExternalSearchPage; filters: ProviderFilters; }
export interface DiscoveryResultProps {
  item: ExternalMedia; identity: ProviderIdentity; id: string; checking: boolean; hidden: boolean;
  onResolved: (id: string, available: boolean) => void;
}
