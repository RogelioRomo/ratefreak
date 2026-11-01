import type { ProviderFilters } from "./provider";
export interface DiscoveryState { filters: ProviderFilters | null; submitted: boolean; error: string | null; }
