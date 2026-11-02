import type { FieldType } from "./contents";
export interface FormIssues {fields: Record<string,string[]>; summary: string[];}
export type InputResult<T> = {ok:true;value:T} | {ok:false;issues:FormIssues};
export interface FieldDraft {key:string;label:string;fieldType:FieldType;required:boolean;position:string;maxLength:string;min:string;max:string;options:string[];}
export interface EntryValueDraft {set:boolean;value:string|boolean;}
