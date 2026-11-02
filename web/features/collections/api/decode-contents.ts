import type { CollectionDetails, CollectionField, CollectionPage, CollectionSummary } from "../interfaces/collections";
import type { CollectionItem, CollectionItemPage, DeletionImpact, FieldConfiguration, FieldType, FieldValue } from "../interfaces/contents";
import { decimalText, isDecimalValue, compareDecimals } from "../query/exact-decimal";
import { parseExactJson } from "./exact-json";
function object(value: unknown): Record<string,unknown> {if(!value || typeof value!=="object" || Array.isArray(value) || isDecimalValue(value))throw new Error("Expected object.");return value as Record<string,unknown>;}
function text(value: unknown): string {if(typeof value!=="string")throw new Error("Expected text.");return value;}
function bool(value: unknown): boolean {if(typeof value!=="boolean")throw new Error("Expected boolean.");return value;}
function array(value: unknown): unknown[] {if(!Array.isArray(value))throw new Error("Expected array.");return value;}
function integer(value: unknown, min=0, max=Number.MAX_SAFE_INTEGER): number {if(!isDecimalValue(value))throw new Error("Expected integer.");const raw=decimalText(value);if(!/^-?\d+$/.test(raw))throw new Error("Expected integer.");const n=Number(raw);if(!Number.isSafeInteger(n)||n<min||n>max)throw new Error("Unsafe integer.");return n;}
function summary(input: unknown): CollectionSummary {
 const v=object(input),owner=object(v.owner);const visibility=text(v.visibility);if(!['private','public'].includes(visibility))throw new Error("Invalid visibility.");
 return {id:text(v.id).toLowerCase(),owner:{id:text(owner.id).toLowerCase(),handle:text(owner.handle),displayName:text(owner.displayName)},name:text(v.name),description:v.description===null?null:text(v.description),visibility:visibility as CollectionSummary['visibility'],revision:integer(v.revision,1),schemaVersion:integer(v.schemaVersion,1),createdAt:text(v.createdAt),updatedAt:text(v.updatedAt)};
}
export function field(input: unknown): CollectionField {
 const v=object(input),type=text(v.fieldType) as FieldType,c=object(v.configuration),config:FieldConfiguration={};
 const allowed=type==='text'?['maxLength']:type==='number'?['min','max']:type==='select'?['options']:['boolean','date'].includes(type)?[]:null;
 if(!allowed || Object.keys(c).some(k=>!allowed.includes(k)))throw new Error("Invalid configuration.");
 if(type==='text' && c.maxLength!==undefined)config.maxLength=integer(c.maxLength,1,4000);
 if(type==='number'){for(const key of ['min','max'] as const){if(c[key]!==undefined){if(!isDecimalValue(c[key]))throw new Error("Invalid number bound.");config[key]=c[key];}}if(config.min&&config.max&&compareDecimals(decimalText(config.min),decimalText(config.max))>0)throw new Error("Inverted bounds.");}
 if(type==='select'){config.options=array(c.options).map(text);if(!config.options.length||config.options.length>50||new Set(config.options).size!==config.options.length||config.options.some(o=>!o.trim()||o.length>80))throw new Error("Invalid options.");}
 return {id:text(v.id).toLowerCase(),key:text(v.key),label:text(v.label),fieldType:type,required:bool(v.required),configuration:config,position:integer(v.position,0,2147483647)};
}
function item(input: unknown): CollectionItem {
 const v=object(input),data=object(v.data),values:CollectionItem['data']=Object.create(null) as CollectionItem['data'];
 for(const [key,value] of Object.entries(data)){if(value!==null && typeof value!=='string' && typeof value!=='boolean' && !isDecimalValue(value))throw new Error("Unsupported field value.");values[key]=value as FieldValue;}
 return {id:text(v.id).toLowerCase(),mediaItemId:v.mediaItemId===null?null:text(v.mediaItemId).toLowerCase(),title:text(v.title),data:values,position:integer(v.position,0,2147483647),createdAt:text(v.createdAt),updatedAt:text(v.updatedAt)};
}
export function decodeDetails(raw: string): CollectionDetails {const v=object(parseExactJson(raw));return {collection:summary(v.collection),fields:array(v.fields).map(field)};}
export function decodeCollectionPage(raw: string): CollectionPage {const v=object(parseExactJson(raw));return {items:array(v.items).map(summary),page:integer(v.page,1,10000),pageSize:integer(v.pageSize,1,100),totalCount:integer(v.totalCount)};}
export function decodeItem(raw: string): CollectionItem {return item(parseExactJson(raw));}
export function decodeItems(raw: string): CollectionItemPage {const v=object(parseExactJson(raw));return {items:array(v.items).map(item),page:integer(v.page,1,10000),pageSize:integer(v.pageSize,1,100),totalCount:integer(v.totalCount)};}
export function decodeField(raw: string): CollectionField {return field(parseExactJson(raw));}
export function decodeImpact(raw: string): DeletionImpact {const v=object(parseExactJson(raw));return {fieldId:text(v.fieldId).toLowerCase(),key:text(v.key),populatedItemCount:integer(v.populatedItemCount),revision:integer(v.revision,1)};}
