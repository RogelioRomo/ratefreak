import type { CollectionDetails, CollectionField } from './collections';
import type { CollectionItem } from './contents';
import type { FieldDraft } from './form-input';
import type { ConfirmedWrite, EntrySnapshot, PageSnapshot } from './snapshots';
export interface ContentsActions {
 execute<T>(mutation:(signal:AbortSignal)=>Promise<T>,refresh:(signal:AbortSignal)=>Promise<void>):Promise<ConfirmedWrite<T>|null>;
 retryRefresh:()=>Promise<void>;needsRefresh:boolean;pending:boolean;error:Error|null;outcomeUnknown:boolean;clearError:()=>void;
}
export interface ActionEditorProps {active:boolean;actions:ContentsActions;refresh:(signal:AbortSignal)=>Promise<void>;onCancel:()=>void;onSaved:()=>void;}
export interface FieldEditorProps extends ActionEditorProps {details:CollectionDetails;field?:CollectionField;itemCount:number;reload:(signal:AbortSignal)=>Promise<CollectionDetails>;}
export interface FieldConfigurationProps {value:FieldDraft;onChange:(value:FieldDraft)=>void;disabled?:boolean;errors?:string[];}
export interface FieldManagerProps {fields:CollectionField[];owner:boolean;locked:boolean;onAdd:()=>void;onEdit:(field:CollectionField)=>void;onRemove:(field:CollectionField)=>void;}
export interface ContentsErrorProps {error:Error|null;summary?:string[];omitValidation?:boolean;}
export interface CollectionWorkspaceProps {id:string;viewer:string;accountId:string|null;active:boolean;}
export interface CatalogSelection {id:string;title:string;}
export interface CatalogPickerProps {value:CatalogSelection|null;onChange:(value:CatalogSelection|null)=>void;disabled?:boolean;}
export interface EntryEditorProps extends ActionEditorProps {onUnavailable?:()=>void;details:CollectionDetails;item?:CollectionItem;reload:(signal:AbortSignal)=>Promise<EntrySnapshot|PageSnapshot>;}
export interface EntryListProps {collectionId:string;items:CollectionItem[];}
export interface EntryValuesProps {fields:CollectionField[];data:CollectionItem['data'];}
export interface EntryDetailProps {collectionId:string;itemId:string;}
export interface EntryPageProps {params:Promise<{id:string;itemId:string}>;}
export interface FieldRemovalProps extends ActionEditorProps {collectionId:string;field:CollectionField;revision:number;}
export interface EntryRemovalProps extends ActionEditorProps {snapshot:EntrySnapshot;reload:(signal:AbortSignal)=>Promise<EntrySnapshot>;}
export type OwnerEditor = {kind:'metadata'|'add-field'|'add-entry';snapshot:PageSnapshot} | {kind:'edit-field'|'remove-field';snapshot:PageSnapshot;field:CollectionField};
