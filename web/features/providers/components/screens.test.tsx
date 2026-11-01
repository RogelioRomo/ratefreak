// @vitest-environment jsdom
import { act, fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { SessionContext } from "@/features/auth/session/session-context";
import { DiscoveryScreen } from "./discovery-screen";
import { ProviderPreviewScreen } from "./provider-preview-screen";
const route=vi.hoisted(()=>({query:'',push:vi.fn()}));
vi.mock('next/navigation',()=>({useSearchParams:()=>new URLSearchParams(route.query),useRouter:()=>({push:route.push})}));
const caps=[{name:'tmdb',enabled:true,supportedTypes:['movie','tv_show'],attribution:'This product uses the TMDB API but is not endorsed or certified by TMDB.',attributionUrl:'https://evil.test'},{name:'musicbrainz',enabled:true,supportedTypes:['album'],attribution:'MusicBrainz metadata',attributionUrl:'https://musicbrainz.org'},{name:'igdb',enabled:false,supportedTypes:['video_game'],attribution:'IGDB',attributionUrl:'https://www.igdb.com'}];
const item={provider:'tmdb',externalId:'11',type:'movie',title:'A Film',description:null,releaseDate:null,imageReference:null,metadata:{}};
const session={account:null,isLoading:false,isTransitioning:false,error:null,refresh:vi.fn(),runExclusive:vi.fn(),changeIdentity:vi.fn()};
const fetchMock=vi.fn<typeof fetch>();
function setup(child=<DiscoveryScreen/>){const client=new QueryClient({defaultOptions:{queries:{retry:false,gcTime:0}}});return render(child,{wrapper:({children})=><QueryClientProvider client={client}><SessionContext.Provider value={session}>{children}</SessionContext.Provider></QueryClientProvider>});}
beforeEach(()=>{route.query='';route.push.mockReset();fetchMock.mockReset();vi.stubGlobal('fetch',fetchMock);fetchMock.mockImplementation(async url=>String(url).endsWith('/providers')?Response.json(caps):String(url).includes('/external/search')?Response.json({provider:'tmdb',page:1,items:[item]}):Response.json(item));});
it('shows initial guidance and capabilities without external search',async()=>{
 setup();expect(await screen.findByText('Search a provider to find your next title.')).toBeInTheDocument();
 expect(fetchMock.mock.calls.every(([url])=>String(url).endsWith('/providers'))).toBe(true);
 expect(screen.getByRole('option',{name:'IGDB (unavailable)'})).toBeDisabled();
});
it('submits normalized filters, resets page and adapts draft types',async()=>{
 route.query='q=old&provider=tmdb&type=movie&page=2';setup();await screen.findByText('A Film');
 fireEvent.change(screen.getByLabelText('Provider'),{target:{value:'musicbrainz'}});
 expect(screen.getByLabelText('Media type')).toHaveValue('album');
 fireEvent.change(screen.getByRole('searchbox'),{target:{value:'  A & B  '}});
 fireEvent.click(screen.getByRole('button',{name:'Search providers'}));
 expect(route.push).toHaveBeenCalledWith('/catalog/discover?q=A+%26+B&provider=musicbrainz&type=album&page=1');
});
it('restores URL filters and isolates late responses',async()=>{
 let resolve!:(r:Response)=>void;
 fetchMock.mockImplementation(url=>String(url).endsWith('/providers')?Promise.resolve(Response.json(caps)):String(url).includes('q=old')?new Promise(r=>{resolve=r;}):Promise.resolve(Response.json({provider:'tmdb',page:1,items:[{...item,title:'New result'}]})));
 route.query='q=old&provider=tmdb&type=movie&page=2';const view=setup();await screen.findByLabelText('Title');
 route.query='q=new&provider=tmdb&type=tv_show&page=1';view.rerender(<DiscoveryScreen/>);
 expect(await screen.findByDisplayValue('new')).toBeInTheDocument();expect(await screen.findByText('New result')).toBeInTheDocument();
 await act(async()=>resolve(Response.json({provider:'tmdb',page:2,items:[item]})));expect(screen.queryByText('A Film')).not.toBeInTheDocument();
});
it('links results to previews and does not invent totals',async()=>{route.query='q=film';setup();expect(await screen.findByRole('link',{name:'A Film'})).toHaveAttribute('href','/catalog/discover/tmdb/movie/11');expect(screen.getByText('1 result on this page')).toBeInTheDocument();expect(screen.getByRole('link',{name:/next/i})).toHaveAttribute('href','/catalog/discover?q=film&provider=tmdb&type=movie&page=2');expect(screen.getByRole('link',{name:'TMDB attribution'})).toHaveAttribute('href','https://www.themoviedb.org');});
it.each(['q=x&q=y','q=x&provider=igdb&type=video_game'])('rejects invalid/disabled URL %s without external request',async q=>{route.query=q;setup();await screen.findByRole('alert');expect(fetchMock.mock.calls.every(([url])=>String(url).endsWith('/providers'))).toBe(true);});
it('shows no providers and retryable capability failures',async()=>{fetchMock.mockResolvedValue(Response.json(caps.map(x=>({...x,enabled:false}))));setup();expect(await screen.findByRole('alert')).toHaveTextContent('No metadata providers');});
it('recovers from an empty later page',async()=>{route.query='q=empty&provider=tmdb&type=movie&page=3';fetchMock.mockImplementation(async url=>String(url).endsWith('/providers')?Response.json(caps):Response.json({provider:'tmdb',page:3,items:[]}));setup();expect(await screen.findByText('No titles on this page.')).toBeInTheDocument();expect(screen.getByRole('link',{name:'Go to first page'})).toHaveAttribute('href','/catalog/discover?q=empty&provider=tmdb&type=movie&page=1');});
it('previews honest fields and offers login continuation',async()=>{setup(<ProviderPreviewScreen provider="tmdb" type="movie" externalId="11"/>);expect(await screen.findByRole('heading',{level:1,name:'A Film'})).toBeInTheDocument();expect(screen.getByText('No description available yet.')).toBeInTheDocument();expect(screen.getByRole('link',{name:'Log in to import'})).toHaveAttribute('href','/login?returnTo=%2Fcatalog%2Fdiscover%2Ftmdb%2Fmovie%2F11');});
it.each([404,429,503])('shows preview %s honestly',async status=>{fetchMock.mockImplementation(async url=>String(url).endsWith('/providers')?Response.json(caps):Response.json({}, {status}));setup(<ProviderPreviewScreen provider="tmdb" type="movie" externalId="11"/>);expect(await screen.findByRole('alert')).toHaveTextContent(status===404?'Title not found':status===429?'Too many requests':'Provider unavailable');expect(screen.getByRole('heading',{level:1,name:'Provider preview.'})).toBeInTheDocument();});
