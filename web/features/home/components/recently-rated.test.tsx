// @vitest-environment jsdom
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, expect, it, vi } from 'vitest';
import Home from '@/app/page';
import { SessionContext } from '@/features/auth/session/session-context';
import type { SessionContextValue } from '@/features/auth/interfaces/session-context-value';
vi.mock('next/navigation',()=>({useSearchParams:()=>new URLSearchParams(),usePathname:()=> '/',useRouter:()=>({push:vi.fn()})}));
const account={id:'owner',handle:'listener',displayName:'Listener',email:'listener@example.test',createdAt:'2026-01-01'};
const cover='https://coverartarchive.org/release-group/3bd76d40-7f0e-36b7-9348-91a33afee20e/front-500';
const rated={media:{id:'a1111111-1111-1111-1111-111111111111',type:'album',title:'Year Zero',releaseDate:null,imageReference:cover},score:0,updatedAt:'2026-11-27T12:00:00Z'};
let session:SessionContextValue;let fetchMock:ReturnType<typeof vi.fn>;
function setup(){const client=new QueryClient({defaultOptions:{queries:{retry:false,gcTime:0}}});const tree=()=> <QueryClientProvider client={client}><SessionContext.Provider value={session}><Home/></SessionContext.Provider></QueryClientProvider>;const view=render(tree());return {client,...view,update:()=>view.rerender(tree())};}
beforeEach(()=>{session={account,error:null,isLoading:false,isTransitioning:false,refresh:vi.fn(async()=>{}),runExclusive:async<T,>(fn:()=>Promise<T>)=>fn(),changeIdentity:async()=>{}};fetchMock=vi.fn(async(url:unknown)=>Response.json(String(url).includes('/ratings/recent')?{items:[rated]}:{items:[],nextCursor:null,hasFollowees:true}));vi.stubGlobal('fetch',fetchMock);});
it('shows private recent ratings with artwork before Following and Community',async()=>{
 setup();const section=await screen.findByRole('region',{name:'Recently rated'});
 expect(await within(section).findByRole('link',{name:/Year Zero/})).toHaveAttribute('href','/catalog/'+rated.media.id);
 expect(within(section).getByRole('img',{name:'Artwork for Year Zero'})).toHaveAttribute('src',cover);
 expect(within(section).getByText('Your rating: 0.0 / 5')).toBeVisible();
 expect(section.compareDocumentPosition(screen.getByRole('navigation',{name:'Feed'})) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
});
it('shows useful empty state without blocking the feeds',async()=>{
 fetchMock.mockImplementation(async(url:unknown)=>Response.json(String(url).includes('/ratings/recent')?{items:[]}:{items:[],nextCursor:null,hasFollowees:true}));setup();
 expect(await screen.findByText('Rate something to start your collection of opinions.')).toBeVisible();
 expect(screen.getByRole('link',{name:'Following'})).toBeVisible();
});
it('conceals stale ratings on refetch failure and offers independent retry',async()=>{
 const view=setup();await screen.findByText('Your rating: 0.0 / 5');
 fetchMock.mockImplementation(async(url:unknown)=>String(url).includes('/ratings/recent')?Response.json({}, {status:503}):Response.json({items:[],nextCursor:null,hasFollowees:true}));
 await act(async()=>{await view.client.refetchQueries({queryKey:['personal','owner','recent-ratings']});});
 await waitFor(()=>expect(screen.queryByRole('link',{name:/Year Zero/})).not.toBeInTheDocument());
 const section=screen.getByRole('region',{name:'Recently rated'});expect(within(section).getByText('Recently rated unavailable.')).toBeVisible();
 fetchMock.mockImplementation(async()=>Response.json({items:[rated]}));fireEvent.click(within(section).getByRole('button',{name:'Retry'}));
 expect(await screen.findByText('Your rating: 0.0 / 5')).toBeVisible();
});
it('does not reuse private ratings across account transitions, errors or logout',async()=>{
 const view=setup();await screen.findByText('Your rating: 0.0 / 5');fetchMock.mockImplementation(()=>new Promise(()=>{}));
 session={...session,account:{...account,id:'other'}};view.update();expect(screen.queryByText('Your rating: 0.0 / 5')).not.toBeInTheDocument();
 session={...session,account:undefined,isTransitioning:true};view.update();expect(screen.queryByRole('region',{name:'Recently rated'})).not.toBeInTheDocument();
 session={...session,isTransitioning:false,error:new Error('session')};view.update();expect(screen.queryByRole('region',{name:'Recently rated'})).not.toBeInTheDocument();
 session={...session,error:null,account:null};view.update();expect(screen.queryByRole('region',{name:'Recently rated'})).not.toBeInTheDocument();
});
it('refreshes the session after an unauthorized recent-rating request',async()=>{
 fetchMock.mockImplementation(async(url:unknown)=>String(url).includes('/ratings/recent')?Response.json({}, {status:401}):Response.json({items:[],nextCursor:null,hasFollowees:true}));setup();
 await waitFor(()=>expect(session.refresh).toHaveBeenCalledTimes(1));expect(screen.queryByText('Your rating: 0.0 / 5')).not.toBeInTheDocument();
});
