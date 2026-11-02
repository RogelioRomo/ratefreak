import { decimalText, decimalValue, isDecimalValue } from "../query/exact-decimal";
export function validUnicode(text: string): boolean {
  for(let i=0;i<text.length;i++) {const c=text.charCodeAt(i);if(c>=0xd800 && c<=0xdbff){const next=text.charCodeAt(++i);if(!(next>=0xdc00 && next<=0xdfff))return false;}else if(c>=0xdc00 && c<=0xdfff)return false;}
  return true;
}
export function parseExactJson(text: string): unknown {
  let at = 0;
  const space = () => {while(/[\t\r\n ]/.test(text[at] ?? "") && at < text.length) at++;};
  function string(): string {
    const start = at++;
    while(at<text.length) {const ch=text[at++];if(ch==='\\'){at++;continue;}if(ch==='"'){const value=JSON.parse(text.slice(start,at)) as string;if(!validUnicode(value))throw new Error("Invalid Unicode.");return value;}}
    throw new Error("Unterminated string.");
  }
  function value(depth: number): unknown {
    if(depth>20)throw new Error("JSON is too deeply nested.");space();const ch=text[at];
    if(ch==='"')return string();
    if(ch==='{' || ch==='[') {
      const object=ch==='{';at++;space();const end=object?'}':']';
      const result: Record<string,unknown> | unknown[] = object ? Object.create(null) as Record<string,unknown> : [];
      if(text[at]===end){at++;return result;}
      while(at<text.length) {
        space();let key='';if(object){if(text[at]!=='"')throw new Error("Expected property.");key=string();if(Object.hasOwn(result,key))throw new Error("Duplicate JSON property.");space();if(text[at++]!==':')throw new Error("Expected colon.");}
        const parsed=value(depth+1);if(object)(result as Record<string,unknown>)[key]=parsed;else (result as unknown[]).push(parsed);
        space();const delimiter=text[at++];if(delimiter===end)return result;if(delimiter!==',')throw new Error("Expected delimiter.");
      }
      throw new Error("Unterminated JSON.");
    }
    for(const [literal,parsed] of [["true",true],["false",false],["null",null]] as const) if(text.startsWith(literal,at)){at+=literal.length;return parsed;}
    const number=/^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/.exec(text.slice(at));
    if(number){at+=number[0].length;return decimalValue(number[0]);}
    throw new Error("Invalid JSON value.");
  }
  const result=value(0);space();if(at!==text.length)throw new Error("Trailing JSON data.");return result;
}
export function stringifyExactJson(input: unknown): string {
  const visited=new Set<object>();
  function encode(value: unknown,depth: number): string {
    if(depth>20)throw new Error("JSON is too deeply nested.");
    if(isDecimalValue(value))return decimalText(value);
    if(value===null || typeof value==='boolean')return String(value);
    if(typeof value==='string'){if(!validUnicode(value))throw new Error("Invalid Unicode.");return JSON.stringify(value);}
    if(typeof value==='number'){if(!Number.isSafeInteger(value))throw new Error("Unsafe JSON integer.");return String(value);}
    if(typeof value!=='object' || visited.has(value))throw new Error("Unsupported JSON value.");
    visited.add(value);
    let result:string;
    if(Array.isArray(value))result=`[${value.map(item=>encode(item,depth+1)).join(',')}]`;
    else {if(Object.getPrototypeOf(value)!==Object.prototype && Object.getPrototypeOf(value)!==null)throw new Error("Unsupported JSON object.");result=`{${Object.entries(value).map(([key,item])=>`${JSON.stringify(key)}:${encode(item,depth+1)}`).join(',')}}`;}
    visited.delete(value);return result;
  }
  return encode(input,0);
}
