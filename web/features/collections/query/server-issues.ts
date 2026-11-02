import { ApiError } from '@/shared/api/api-error';
import type { FormIssues } from '../interfaces/form-input';
import type { CollectionField } from '../interfaces/collections';
function issues(error:Error|null,map:(key:string)=>string|null):FormIssues{
 const result:FormIssues={fields:{},summary:[]};if(!(error instanceof ApiError))return result;
 for(const [key,messages] of Object.entries(error.problem?.errors??{})){const target=map(key);if(target)result.fields[target]=[...(result.fields[target]??[]),...messages];else result.summary.push(...messages);}
 return result;
}
export function fieldServerIssues(error:Error|null,count:number,editing:boolean):FormIssues{return issues(error,key=>{
 if(['key','label','fieldType','required','position','configuration'].includes(key))return key;
 const match=/^fields\[(\d+)\]\.(key|label|fieldType|required|position|configuration)(?:\..*)?$/.exec(key);
 if(match&&(editing||+match[1]===count))return match[2];return null;
});}
export function entryServerIssues(error:Error|null,fields:CollectionField[]):FormIssues{return issues(error,key=>['title','position'].includes(key)?key:key.startsWith('data.')&&fields.some(f=>`data.${f.key}`===key)?key:null);}
