import { expect, it } from "vitest";
import { providerIdentity, validateProviderSearch } from "./provider-policy";
import { isAllowedRoute } from "./route-policy";
it.each([['tmdb','movie','00011','11'],['igdb','video_game','1942','1942'],['musicbrainz','album','A1111111-1111-1111-1111-111111111111','a1111111-1111-1111-1111-111111111111']])('canonicalizes %s', (provider,type,id,result)=>expect(providerIdentity(provider,type,id)?.externalId).toBe(result));
it.each([['tmdb','album','11'],['igdb','movie','11'],['tmdb','movie','2147483648'],['tmdb','movie','0'],['tmdb','movie','-1'],['musicbrainz','album','00000000-0000-0000-0000-000000000000'],['unknown','movie','11']])('rejects identity %s/%s/%s',(p,t,id)=>expect(providerIdentity(p,t,id)).toBeNull());
it('validates and encodes external search',()=>expect(validateProviderSearch(new URLSearchParams('q=++A+%26+B++&provider=tmdb&type=movie&page=2')).toString()).toBe('q=A+%26+B&provider=tmdb&type=movie&page=2'));
it.each(['q=a&q=b&provider=tmdb&type=movie','q=x&provider=tmdb&type=album','q=x&provider=tmdb&type=movie&page=501','q=%00&provider=tmdb&type=movie','q=x%0A&provider=tmdb&type=movie','q=x&provider=tmdb&type=movie&target=x','q=&provider=tmdb&type=movie'])('rejects %s',q=>expect(()=>validateProviderSearch(new URLSearchParams(q))).toThrow());
it('allows only exact provider routes/methods',()=>{
 for(const path of [['media','providers'],['media','external','search'],['media','external','tmdb','movie','11']])expect(isAllowedRoute('GET',path)).toBe(true);
 expect(isAllowedRoute('POST',['media','import'])).toBe(true);
 expect(isAllowedRoute('GET',['media','import'])).toBe(false);
 expect(isAllowedRoute('POST',['media','external','search'])).toBe(false);
 expect(isAllowedRoute('GET',['media','external','tmdb','album','11'])).toBe(false);
});
