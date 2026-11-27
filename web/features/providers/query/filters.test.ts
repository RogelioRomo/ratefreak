import { expect, it } from "vitest";
import { parseDiscovery, discoveryHref } from "./filters";
const caps=[{name:'tmdb',enabled:true,supportedTypes:['movie','tv_show'],attribution:'TMDB',attributionUrl:'https://www.themoviedb.org'}] as const;
it('defaults to guidance without an external search',()=>expect(parseDiscovery(new URLSearchParams(),JSON.parse(JSON.stringify(caps)))).toMatchObject({filters:{provider:'tmdb',type:'movie',q:'',page:1},submitted:false,error:null}));
it.each(['q=a&q=b','q=x&provider=igdb&type=video_game','q=x&provider=tmdb&type=album','q=%00','q=x&page=501','target=x'])('rejects %s',q=>expect(parseDiscovery(new URLSearchParams(q),JSON.parse(JSON.stringify(caps))).error).toBeTruthy());
it('serializes complete normalized search',()=>expect(discoveryHref({q:'A & B',provider:'tmdb',type:'movie',page:2})).toBe('/catalog/discover?q=A+%26+B&provider=tmdb&type=movie&page=2'));
it("accepts an enabled Open Library capability and selects Books", () => {
 const books = [{ name: "openlibrary", enabled: true, supportedTypes: ["book"], attribution: "Open Library", attributionUrl: "https://openlibrary.org" }];
 expect(parseDiscovery(new URLSearchParams("q=Fantastic+Mr+Fox&provider=openlibrary&type=book"), JSON.parse(JSON.stringify(books))))
   .toMatchObject({ filters: { provider: "openlibrary", type: "book", q: "Fantastic Mr Fox", page: 1 }, submitted: true, error: null });
});
it.each(['album','song'])('defaults MusicBrainz %s to verified artwork and persists opt-out',type=>{
 const music=[{name:'musicbrainz',enabled:true,supportedTypes:['album','song'],attribution:'MusicBrainz',attributionUrl:'https://musicbrainz.org'}];
 const defaults=parseDiscovery(new URLSearchParams(`q=music&provider=musicbrainz&type=${type}`),JSON.parse(JSON.stringify(music)));
 expect(defaults.filters).toMatchObject({artworkOnly:true});
 const all=parseDiscovery(new URLSearchParams(`q=music&provider=musicbrainz&type=${type}&artwork=all`),JSON.parse(JSON.stringify(music)));
 expect(all.filters).toMatchObject({artworkOnly:false});
 expect(discoveryHref(all.filters!)).toContain('artwork=all');
});
it('rejects unknown and duplicate artwork filter values',()=>{
 const music=[{name:'musicbrainz',enabled:true,supportedTypes:['album'],attribution:'MusicBrainz',attributionUrl:'https://musicbrainz.org'}];
 for(const tail of ['artwork=maybe','artwork=all&artwork=only'])expect(parseDiscovery(new URLSearchParams(`q=x&provider=musicbrainz&type=album&${tail}`),JSON.parse(JSON.stringify(music))).error).toBeTruthy();
});
