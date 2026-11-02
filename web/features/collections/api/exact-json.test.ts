import { expect, it } from "vitest";
import { parseExactJson, stringifyExactJson } from "./exact-json";
import { decimalText, decimalValue } from "../query/exact-decimal";
it('round trips dynamic numeric tokens and literal tag-like values',()=>{
 const text='{"n":9007199254740993,"fraction":0.1234567890123456789012345678,"zero":0,"false":false,"empty":"","nil":null,"literal":{"text":"9007199254740993"},"escaped":"\\\"\\n"}';
 const value=parseExactJson(text) as Record<string,unknown>;
 expect(decimalText(value.n as ReturnType<typeof decimalValue>)).toBe('9007199254740993');expect(stringifyExactJson(value)).toBe(text);
 expect(stringifyExactJson({text:'9007199254740993'})).toBe('{"text":"9007199254740993"}');
 expect(stringifyExactJson(['</script>',decimalValue('1e-28')])).toBe('["</script>",0.0000000000000000000000000001]');
});
it.each(['{"a":1,"a":2}','{"a":01}','[1,]','{"a":"\\ud800"}','1 trailing','{"__proto__":true,"constructor":false,}'])('rejects malformed JSON %s',text=>expect(()=>parseExactJson(text)).toThrow());
it('retains special keys as own values without prototype mutation',()=>{const v=parseExactJson('{"__proto__":"literal","constructor":"text"}');expect(stringifyExactJson(v)).toBe('{"__proto__":"literal","constructor":"text"}');});
it('rejects cyclic, nonfinite and unsafe ordinary numeric encoding',()=>{expect(()=>stringifyExactJson({n:NaN})).toThrow();expect(()=>stringifyExactJson({n:9007199254740992})).toThrow();const a:unknown[]=[];a.push(a);expect(()=>stringifyExactJson(a)).toThrow();});
it('preserves exact token identity through QueryClient structural sharing on changed values',async()=>{
 const {QueryClient}=await import('@tanstack/react-query');const client=new QueryClient();
 client.setQueryData(['value'],parseExactJson('{"n":1.1}'));client.setQueryData(['value'],parseExactJson('{"n":1.2}'));
 expect(stringifyExactJson(client.getQueryData(['value']))).toBe('{"n":1.2}');client.clear();
});
