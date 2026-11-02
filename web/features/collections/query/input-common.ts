import { validUnicode } from "../api/exact-json";
export function validText(value:string,max:number,multiline=false,blank=false):boolean{return value.length<=max&&(blank||!!value.trim())&&validUnicode(value)&&!new RegExp(multiline?'[\\x00-\\x08\\x0b\\x0c\\x0e-\\x1f\\x7f-\\x9f]':'[\\x00-\\x1f\\x7f-\\x9f]').test(value);}
export function positionValue(value:string):number|null{const n=Number(value);return /^\d+$/.test(value)&&Number.isInteger(n)&&n>=0&&n<=2147483647?n:null;}
