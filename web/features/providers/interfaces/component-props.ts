import type { ProviderCapability, ProviderFilters, ProviderIdentity } from "./provider";
export interface DiscoveryFormProps { capabilities: ProviderCapability[]; filters: ProviderFilters; }
export interface ProviderAttributionProps { capability: ProviderCapability; }
export interface ProviderPreviewProps { provider: string; type: string; externalId: string; }
export interface ProviderPreviewPageProps { params: Promise<ProviderPreviewProps>; }
export interface ImportControlProps { identity: ProviderIdentity; }
