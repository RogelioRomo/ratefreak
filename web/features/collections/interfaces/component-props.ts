import type { ReactNode } from "react";
import type { CollectionDetails, CollectionMetadata, CollectionSummary } from "./collections";
export interface CollectionSessionState { viewer: string; accountId: string | null; active: boolean; }
export interface CollectionSessionProps { children: (state: CollectionSessionState) => ReactNode; requireAccount?: boolean; subject?: string; returnTo: string; }
export interface CollectionListProps { items: CollectionSummary[]; }
export interface CollectionDetailProps { id: string; }
export interface CollectionPageProps { params: Promise<{id: string}>; }
export interface MetadataFieldsProps { value: CollectionMetadata; onChange: (value: CollectionMetadata) => void; errors: Record<string,string[]>; disabled?: boolean; }
export interface CollectionEditorProps { onUnavailable?: () => void; reload?: (signal: AbortSignal) => Promise<CollectionDetails>; saved: CollectionDetails; accountId: string; active: boolean; onCancel: () => void; onSaved: (details: CollectionDetails) => void; }
