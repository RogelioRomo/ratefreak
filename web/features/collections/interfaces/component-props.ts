import type { ReactNode } from "react";
import type { CollectionDetails, CollectionMetadata, CollectionSummary } from "./collections";
export interface CollectionSessionState { viewer: string; accountId: string | null; active: boolean; }
export interface CollectionSessionProps { children: (state: CollectionSessionState) => ReactNode; requireAccount?: boolean; returnTo: string; }
export interface CollectionListProps { items: CollectionSummary[]; }
export interface CollectionDetailProps { id: string; }
export interface CollectionPageProps { params: Promise<{id: string}>; }
export interface MetadataFieldsProps { value: CollectionMetadata; onChange: (value: CollectionMetadata) => void; errors: Record<string,string[]>; disabled?: boolean; }
export interface CollectionEditorProps { reload?: (signal: AbortSignal) => Promise<CollectionDetails>; saved: CollectionDetails; accountId: string; active: boolean; onCancel: () => void; onSaved: (details: CollectionDetails) => void; }

export interface EntryValueControlsProps {fields: import('./collections').CollectionField[]; value: Record<string,import('./form-input').EntryValueDraft>; onChange:(value:Record<string,import('./form-input').EntryValueDraft>)=>void;errors:Record<string,string[]>;disabled?:boolean;}
