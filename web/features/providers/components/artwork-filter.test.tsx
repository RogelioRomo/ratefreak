// @vitest-environment jsdom
import { act, fireEvent, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { ExternalMedia } from '../interfaces/provider';
import { DiscoveryScreen } from './discovery-screen';
const route=vi.hoisted(()=>({query:'',push:vi.fn()}));
vi.mock('next/navigation',()=>({useSearchParams:()=>new URLSearchParams(route.query),useRouter:()=>({push:route.push})}));
const caps=[{name:'musicbrainz',enabled:true,supportedTypes:['album','song'],attribution:'MusicBrainz',attributionUrl:'https://musicbrainz.org'}];
const cover='https://coverartarchive.org/release-group/3bd76d40-7f0e-36b7-9348-91a33afee20e/front-500';
const base:ExternalMedia={provider:'musicbrainz',externalId:'3bd76d40-7f0e-36b7-9348-91a33afee20e',type:'album',title:'Loads a cover',description:null,releaseDate:null,imageReference:cover,metadata:{}};
let items:ExternalMedia[];let fetchMock:ReturnType<typeof vi.fn>;
function setup(){return render(<QueryClientProvider client={new QueryClient({defaultOptions:{queries:{retry:false,gcTime:0}}})}><DiscoveryScreen/></QueryClientProvider>);}
beforeEach(()=>{route.query='q=music&provider=musicbrainz&type=album&page=2';route.push.mockReset();items=[base,{...base,externalId:'df86f3ac-6da2-3e5c-b4ba-5f8abb29ce51',title:'Broken cover',imageReference:cover.replace('3bd76d40-7f0e-36b7-9348-91a33afee20e','df86f3ac-6da2-3e5c-b4ba-5f8abb29ce51')},{...base,externalId:'b7ffd2af-418f-4be2-bdd1-22f8b48613da',title:'No cover',imageReference:null}];fetchMock=vi.fn(async(url:unknown)=>Response.json(String(url).endsWith('/providers')?caps:{provider:'musicbrainz',page:2,items}));vi.stubGlobal('fetch',fetchMock);});
afterEach(()=>vi.useRealTimers());
it.each(['album','song'] as const)('defaults %s to showing only successfully loaded images',async type=>{
 route.query=`q=music&provider=musicbrainz&type=${type}&page=2`;items=items.map(item=>({...item,type}));setup();
 expect(await screen.findByRole('checkbox',{name:'Only show results with artwork'})).toBeChecked();
 const image=await screen.findByAltText('Artwork for Loads a cover');expect(image).toHaveAttribute('loading','eager');
 expect(screen.queryByRole('link',{name:'Loads a cover'})).not.toBeInTheDocument();
 fireEvent.load(image);fireEvent.error(screen.getByAltText('Artwork for Broken cover'));
 expect(await screen.findByRole('link',{name:'Loads a cover'})).toBeVisible();
 expect(screen.queryByRole('link',{name:'Broken cover'})).not.toBeInTheDocument();expect(screen.queryByRole('link',{name:'No cover'})).not.toBeInTheDocument();
 expect(screen.getByRole('link',{name:'Next'})).toHaveAttribute('href',`/catalog/discover?q=music&provider=musicbrainz&type=${type}&page=3`);
});
it('shows all results when opted out and never forwards the UI setting to the API',async()=>{
 route.query+='&artwork=all';setup();expect(await screen.findByRole('link',{name:'No cover'})).toBeVisible();
 expect(screen.getByRole('checkbox',{name:'Only show results with artwork'})).not.toBeChecked();expect(screen.getByAltText('Artwork for Loads a cover')).toHaveAttribute('loading','lazy');
 expect(fetchMock.mock.calls.filter(([url])=>String(url).includes('/external/search')).every(([url])=>!String(url).includes('artwork='))).toBe(true);
 expect(screen.getByRole('link',{name:'Next'})).toHaveAttribute('href','/catalog/discover?q=music&provider=musicbrainz&type=album&page=3&artwork=all');
});
it('submits the unchecked filter at page one',async()=>{
 setup();fireEvent.click(await screen.findByRole('checkbox',{name:'Only show results with artwork'}));fireEvent.click(screen.getByRole('button',{name:'Search providers'}));
 expect(route.push).toHaveBeenCalledWith('/catalog/discover?q=music&provider=musicbrainz&type=album&page=1&artwork=all');
});
it('keeps Next and offers Show all results when every cover is missing',async()=>{
 items=items.map(item=>({...item,imageReference:null}));setup();
 expect(await screen.findByText('No covers loaded on this page.')).toBeVisible();
 expect(screen.getByRole('link',{name:'Next'})).toHaveAttribute('href','/catalog/discover?q=music&provider=musicbrainz&type=album&page=3');
 expect(screen.getByRole('link',{name:'Show all results'})).toHaveAttribute('href','/catalog/discover?q=music&provider=musicbrainz&type=album&page=2&artwork=all');
});
it('checks only four covers at a time and advances as checks finish',async()=>{
 items=Array.from({length:6},(_,index)=>({...base,externalId:`00000000-0000-0000-0000-00000000000${index+1}`,title:`Album ${index}`}));setup();
 expect(await screen.findAllByAltText(/Artwork for Album/)).toHaveLength(4);
 fireEvent.load(screen.getByAltText('Artwork for Album 0'));await screen.findByRole('link',{name:'Album 0'});
 expect(screen.getByAltText('Artwork for Album 4')).toBeInTheDocument();expect(screen.queryByAltText('Artwork for Album 5')).not.toBeInTheDocument();
});
it('times out stalled covers and offers recovery instead of checking forever',async()=>{
 vi.useFakeTimers({shouldAdvanceTime:true});items=[base];setup();await screen.findByAltText('Artwork for Loads a cover');
 await act(async()=>{vi.advanceTimersByTime(9000);});
 expect(screen.getByText('No covers loaded on this page.')).toBeVisible();expect(screen.getByRole('link',{name:'Show all results'})).toBeVisible();
});
