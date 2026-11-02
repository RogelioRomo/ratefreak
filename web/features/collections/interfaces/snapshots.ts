import type { CollectionDetails } from "./collections";
import type { CollectionItem, CollectionItemPage } from "./contents";
export interface PageSnapshot {details: CollectionDetails; page: CollectionItemPage;}
export interface EntrySnapshot {details: CollectionDetails; item: CollectionItem;}
export interface ConfirmedWrite<T> {value: T; needsRefresh: boolean;}
