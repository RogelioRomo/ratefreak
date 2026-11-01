import { expect, it } from "vitest";
import { parseDiscovery, discoveryHref } from "./filters";
const caps=[{name:'tmdb',enabled:true,supportedTypes:['movie','tv_show'],attribution:'TMDB',attributionUrl:'https://www.themoviedb.org'}] as const;
it('defaults to guidance without an external search',()=>expect(parseDiscovery(new URLSearchParams(),JSON.parse(JSON.stringify(caps)))).toMatchObject({filters:{provider:'tmdb',type:'movie',q:'',page:1},submitted:false,error:null}));
it.each(['q=a&q=b','q=x&provider=igdb&type=video_game','q=x&provider=tmdb&type=album','q=%00','q=x&page=501','target=x'])('rejects %s',q=>expect(parseDiscovery(new URLSearchParams(q),JSON.parse(JSON.stringify(caps))).error).toBeTruthy());
it('serializes complete normalized search',()=>expect(discoveryHref({q:'A & B',provider:'tmdb',type:'movie',page:2})).toBe('/catalog/discover?q=A+%26+B&provider=tmdb&type=movie&page=2'));
