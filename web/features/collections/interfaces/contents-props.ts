import type { CollectionDetails } from "./collections";
import type { CollectionItem } from "./contents";
import type { ConfirmedWrite } from "./snapshots";
export interface ContentsActions {
 execute<T>(mutation: (signal: AbortSignal) => Promise<T>, refresh: (signal: AbortSignal) => Promise<void>): Promise<ConfirmedWrite<T> | null>;
 retryRefresh: () => Promise<void>; needsRefresh: boolean; pending: boolean; error: Error | null; outcomeUnknown: boolean; clearError: () => void;
}
export interface ContentsErrorProps { error: Error | null; summary?: string[]; omitValidation?: boolean; }
export interface CollectionWorkspaceProps { id: string; viewer: string; accountId: string | null; active: boolean; }
export interface CatalogSelection { id: string; title: string; }
export interface CatalogPickerProps { value: CatalogSelection | null; onChange: (value: CatalogSelection | null) => void; disabled?: boolean; }
export interface EntryDetailProps { collectionId: string; itemId: string; }
export interface EntryPageProps { params: Promise<{ id: string; itemId: string }>; }
export interface MembershipEditorProps { details: CollectionDetails; item?: CollectionItem; active: boolean; actions: ContentsActions; refresh: (signal: AbortSignal) => Promise<void>; onCancel: () => void; onSaved: () => void; }
export interface ListRemovalProps extends MembershipEditorProps { item: CollectionItem; }
