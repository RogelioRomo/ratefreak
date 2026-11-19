// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, expect, it, vi } from 'vitest';
import Home from '@/app/page';
import { SessionContext } from '@/features/auth/session/session-context';
import type { SessionContextValue } from '@/features/auth/interfaces/session-context-value';
const route=vi.hoisted(()=>({query:'',push:vi.fn()}));
vi.mock('next/navigation',()=>({useSearchParams:()=>new URLSearchParams(route.query),usePathname:()=> '/',useRouter:()=>({push:route.push,replace:route.push})}));
const account={id:'owner',handle:'reader',displayName:'Reader',email:'reader@example.test',createdAt:'2026-01-01'};
const entry={author:{id:'author',handle:'alex',displayName:'Alex'},media:{id:'a1111111-1111-1111-1111-111111111111',type:'book',title:'Dune',imageReference:null,releaseDate:null},score:null,review:{id:'review',excerpt:'The ending matters.',isSpoiler:true},publishedAt:'2026-11-18T12:00:00Z'};
let session:SessionContextValue;let fetchMock:ReturnType<typeof vi.fn>;
function setup(){const client=new QueryClient({defaultOptions:{queries:{retry:false,gcTime:0}}});const tree=()=> <QueryClientProvider client={client}><SessionContext.Provider value={session}><Home/></SessionContext.Provider></QueryClientProvider>;const view=render(tree());return {client,...view,update:()=>view.rerender(tree())};}
beforeEach(()=>{route.query='';route.push.mockReset();session={account,error:null,isLoading:false,isTransitioning:false,refresh:vi.fn(),runExclusive:async<T,>(fn:()=>Promise<T>)=>fn(),changeIdentity:async()=>{}};fetchMock=vi.fn(async()=>Response.json({items:[entry],nextCursor:null,hasFollowees:true}));vi.stubGlobal('fetch',fetchMock);});
it('shows signed-in home feeds and conceals spoilers and private scores',async()=>{
 setup();expect(await screen.findByRole('link',{name:'Following'})).toHaveAttribute('aria-current','page');
 expect(screen.getByRole('link',{name:'Community'})).toHaveAttribute('href','/?tab=community');
 expect(await screen.findByRole('link',{name:'Dune'})).toHaveAttribute('href','/catalog/'+entry.media.id);
 expect(screen.queryByText('The ending matters.')).not.toBeInTheDocument();fireEvent.click(screen.getByRole('button',{name:'Show spoilers'}));expect(screen.getByText('The ending matters.')).toBeVisible();
 expect(screen.queryByLabelText(/stars/)).not.toBeInTheDocument();expect(screen.getByRole('link',{name:'Read review and comments'})).toHaveAttribute('href','/reviews/review');
});
it('keeps the introduction for signed-out visitors without reading a feed',()=>{session={...session,account:null};setup();expect(screen.getByRole('link',{name:/Create your account/})).toHaveAttribute('href','/register');expect(fetchMock).not.toHaveBeenCalled();});
it('conceals the old account feed during session transitions and errors',async()=>{const view=setup();await screen.findByRole('link',{name:'Dune'});session={...session,account:undefined,isTransitioning:true};view.update();expect(screen.queryByRole('link',{name:'Dune'})).not.toBeInTheDocument();session={...session,error:new Error('session'),isTransitioning:false};view.update();expect(screen.queryByRole('link',{name:'Dune'})).not.toBeInTheDocument();expect(screen.getByRole('button',{name:'Retry session'})).toBeVisible();});
it('does not show stale feed entries when a refresh fails',async()=>{setup();await screen.findByRole('link',{name:'Dune'});fetchMock.mockResolvedValue(Response.json({}, {status:503}));fireEvent.click(screen.getByRole('button',{name:'Refresh'}));await waitFor(()=>expect(screen.queryByRole('link',{name:'Dune'})).not.toBeInTheDocument());expect(await screen.findByRole('button',{name:'Retry'})).toBeVisible();});
it('opens the selected community/media view and resets paging on filter change',async()=>{route.query='tab=community&type=book';setup();await screen.findByRole('link',{name:'Dune'});expect(String(fetchMock.mock.calls[0][0])).toContain('tab=community&type=book');fireEvent.change(screen.getByLabelText('Media type'),{target:{value:'movie'}});expect(route.push).toHaveBeenCalledWith('/?tab=community&type=movie');});
it('appends bounded pages without duplicate pairs and Back resumes the requested slice',async()=>{
 const next={...entry,media:{...entry.media,id:'b1111111-1111-1111-1111-111111111111',title:'The Left Hand of Darkness'},review:null,score:0};
 fetchMock.mockImplementation(async(url:unknown)=>Response.json(String(url).includes('cursor=next')?{items:[entry,next],nextCursor:null,hasFollowees:true}:{items:[entry],nextCursor:'next',hasFollowees:true}));
 const view=setup();fireEvent.click(await screen.findByRole('button',{name:'Load more'}));expect(route.push).toHaveBeenCalledWith('/?cursor=next');route.query='cursor=next';view.update();
 await screen.findByRole('link',{name:'The Left Hand of Darkness'});expect(screen.getAllByRole('link',{name:'Dune'})).toHaveLength(1);expect(screen.getByLabelText('0 out of 5 stars')).toBeVisible();
 route.query='';view.update();await waitFor(()=>expect(screen.queryByRole('link',{name:'The Left Hand of Darkness'})).not.toBeInTheDocument());
});
it('rejects invalid filters without fetching',()=>{route.query='tab=popular';setup();expect(screen.getByRole('alert')).toHaveTextContent('Invalid feed filters');expect(fetchMock).not.toHaveBeenCalled();});
it('does not reuse the previous account feed when another account signs in',async()=>{
 const view=setup();await screen.findByRole('link',{name:'Dune'});fetchMock.mockImplementation(()=>new Promise(()=>{}));session={...session,account:{...account,id:'other'}};view.update();expect(screen.queryByRole('link',{name:'Dune'})).not.toBeInTheDocument();
});
