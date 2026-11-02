import type { DecimalValue } from "./exact-json";
export type FieldType = "text" | "number" | "boolean" | "date" | "select";
export interface FieldConfiguration { maxLength?: number; min?: DecimalValue; max?: DecimalValue; options?: string[]; }
export type FieldValue = string | boolean | null | DecimalValue;
export interface CollectionItem { id: string; mediaItemId: string | null; title: string; data: Record<string,FieldValue>; position: number; createdAt: string; updatedAt: string; }
export interface CollectionItemPage {items: CollectionItem[]; page: number; pageSize: number; totalCount: number;}
export interface DeletionImpact {fieldId: string; key: string; populatedItemCount: number; revision: number;}
export interface FieldDefinitionInput {key: string; label: string; fieldType: FieldType; required: boolean; configuration: FieldConfiguration; position: number;}
export interface FieldCreateInput {expectedRevision: number; field: FieldDefinitionInput;}
export interface FieldUpdateInput {expectedRevision: number; label: string; required: boolean; configuration: FieldConfiguration; position: number;}
export interface ItemCreateInput {expectedRevision: number; mediaItemId: string | null; title: string | null; data: CollectionItem["data"]; position: number;}
export interface ItemUpdateInput {expectedRevision: number; title: string | null; data: CollectionItem["data"]; position: number;}
