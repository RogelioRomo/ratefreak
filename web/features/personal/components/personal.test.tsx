// @vitest-environment jsdom
import { selectValue } from "@/test/select-value";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { SessionContext } from "@/features/auth/session/session-context";
import type { SessionContextValue } from "@/features/auth/interfaces/session-context-value";
import { resetCsrfState } from "@/shared/api/csrf";
import { PersonalControls } from "./personal-controls";
import { PublicReviews } from "./public-reviews";
import { LibraryScreen } from "./library-screen";
const route=vi.hoisted(()=>({query:'',push:vi.fn(),replace:vi.fn()}));
vi.mock('next/navigation',()=>({useSearchParams:()=>new URLSearchParams(route.query),useRouter:()=>({push:route.push,replace:route.replace})}));
const id='a1111111-1111-1111-1111-111111111111';const account={id:'owner',handle:'reader',displayName:'Reader',email:'reader@example.test',createdAt:'2026-10-31'};
const media={id,type:'movie',title:'A film',releaseDate:null,imageReference:null};
const entry={media,status:'completed',startedAt:'2026-01-01',completedAt:'2026-01-02',createdAt:'2026-01-01',updatedAt:'2026-01-02'};
const review={id:'review-id',mediaId:id,author:{id:'owner',handle:'reader',displayName:'Reader'},body:'<b>Literal text</b>',isSpoiler:true,createdAt:'2026-01-01',updatedAt:'2026-01-02'};
let session:SessionContextValue;const mock=vi.fn<typeof fetch>();
function setup(child:React.ReactNode){const client=new QueryClient({defaultOptions:{queries:{retry:false,gcTime:0}}});const wrap=({children}:{children:React.ReactNode})=><QueryClientProvider client={client}><SessionContext.Provider value={session}>{children}</SessionContext.Provider></QueryClientProvider>;return {...render(child,{wrapper:wrap}),client};}
beforeEach(()=>{route.query='';route.push.mockReset();route.replace.mockReset();resetCsrfState();session={account,isLoading:false,isTransitioning:false,error:null,refresh:vi.fn().mockResolvedValue(undefined),runExclusive:vi.fn(),changeIdentity:vi.fn()};mock.mockReset();vi.stubGlobal('fetch',mock);mock.mockImplementation(async(url,opts)=>{
 const path=String(url);if(path.endsWith('csrf'))return Response.json({requestToken:'token'});
 if(opts?.method==='DELETE')return new Response(null,{status:204});
 if(path.includes('/users/me/library?'))return Response.json({items:[entry],page:1,pageSize:20,totalCount:21});
 if(path.endsWith('/rating'))return Response.json({score:0});
 if(path.endsWith('/review')||path.includes('/reviews/review-id'))return Response.json(review);
 if(path.includes('/reviews?'))return Response.json({items:[review],page:1,pageSize:20,totalCount:21});
 if(path.endsWith('/reviews'))return Response.json(review,{status:201});
 return Response.json(entry);
 });});
it('loads tracking dates and saves them unchanged on status edit',async()=>{setup(<PersonalControls mediaId={id}/>);await screen.findByDisplayValue('2026-01-01');selectValue(screen.getByLabelText('Tracking status'), 'paused');fireEvent.click(screen.getByRole('button',{name:'Save tracking'}));await screen.findByText('Tracking saved.');const call=mock.mock.calls.find(([url,opts])=>String(url).endsWith('/library/'+id)&&opts?.method==='PUT');expect(JSON.parse(String(call?.[1]?.body))).toEqual({status:'paused',startedAt:'2026-01-01',completedAt:'2026-01-02'});});
it('treats zero as a real saved rating',async()=>{setup(<PersonalControls mediaId={id}/>);expect(await screen.findByLabelText('Your score')).toHaveTextContent('0.0 / 5');fireEvent.click(screen.getByRole('button',{name:'Save rating'}));await screen.findByText('Rating saved.');expect(mock.mock.calls.find(([,o])=>o?.method==='PUT'&&o.body==='{"score":0}')).toBeTruthy();});
it('confirms tracking removal and preserves independent resources',async()=>{setup(<PersonalControls mediaId={id}/>);fireEvent.click(await screen.findByRole('button',{name:'Remove from library'}));expect(screen.getByText('Your rating and review will stay.')).toBeInTheDocument();expect(mock.mock.calls.some(([,o])=>o?.method==='DELETE')).toBe(false);fireEvent.click(screen.getByRole('button',{name:'Confirm removal'}));await screen.findByText('Removed from your library.');expect(mock.mock.calls.filter(([,o])=>o?.method==='DELETE')).toHaveLength(1);expect(screen.getByLabelText('Your score')).toHaveTextContent('0.0 / 5');});
it('rejects reversed tracking dates without writing',async()=>{setup(<PersonalControls mediaId={id}/>);await screen.findByLabelText('Completed on');fireEvent.change(screen.getByLabelText('Completed on'),{target:{value:'2025-01-01'}});fireEvent.submit(screen.getByRole('form',{name:'Tracking'}));expect(await screen.findByText('Completion must not precede start.')).toBeInTheDocument();expect(mock.mock.calls.some(([,o])=>o?.method==='PUT')).toBe(false);});
it('edits a review literally and keeps drafts across public page changes/refetch',async()=>{const view=setup(<PersonalControls mediaId={id}/>);const input=await screen.findByLabelText('Your public review');expect(input).toHaveValue('<b>Literal text</b>');fireEvent.change(input,{target:{value:'My unsaved draft'}});route.query='reviewsPage=2';view.rerender(<PersonalControls mediaId={id}/>);await act(async()=>view.client.invalidateQueries({queryKey:['personal','owner','review',id]}));expect(screen.getByLabelText('Your public review')).toHaveValue('My unsaved draft');fireEvent.click(screen.getByRole('button',{name:'Save review'}));await screen.findByText('Review saved.');expect(mock.mock.calls.find(([,o])=>o?.method==='PATCH')?.[1]?.body).toBe('{"body":"My unsaved draft","isSpoiler":true}');});
it('blocks private state on failed reads instead of treating it as absent',async()=>{mock.mockResolvedValue(Response.json({}, {status:503}));setup(<PersonalControls mediaId={id}/>);expect(await screen.findAllByRole('alert')).toHaveLength(3);expect(screen.queryByRole('button',{name:'Add to library'})).not.toBeInTheDocument();});
it('shows login continuation and hides state on session transitions',async()=>{const view=setup(<PersonalControls mediaId={id}/>);await screen.findByDisplayValue('2026-01-01');session={...session,account:undefined};view.rerender(<PersonalControls mediaId={id}/>);expect(screen.getByLabelText('Your public review')).not.toBeVisible();session={...session,account:null};view.rerender(<PersonalControls mediaId={id}/>);expect(screen.getByRole('link',{name:'Log in to track, rate or review'})).toHaveAttribute('href',`/login?returnTo=%2Fcatalog%2F${id}`);});
it('does not leak a late write after changing account',async()=>{let resolve!:(r:Response)=>void;const original=mock.getMockImplementation()!;mock.mockImplementation((url,opts)=>opts?.method==='PUT'?new Promise(r=>{resolve=r;}):original(url,opts));const view=setup(<PersonalControls mediaId={id}/>);fireEvent.click(await screen.findByRole('button',{name:'Save rating'}));await waitFor(()=>expect(resolve).toBeTypeOf('function'));session={...session,account:{...account,id:'other'}};view.rerender(<PersonalControls mediaId={id}/>);await act(async()=>resolve(Response.json({score:5})));expect(screen.queryByText('Rating saved.')).not.toBeInTheDocument();});
it('hides spoiler text until requested and renders plain text',async()=>{setup(<PublicReviews mediaId={id}/>);await screen.findByRole('button',{name:'Show spoilers'});expect(screen.queryByText('<b>Literal text</b>')).not.toBeInTheDocument();fireEvent.click(screen.getByRole('button',{name:'Show spoilers'}));expect(screen.getByText('<b>Literal text</b>')).toBeInTheDocument();expect(document.querySelector('b')).toBeNull();expect(screen.getByRole('link',{name:'Next reviews'})).toHaveAttribute('href',`/catalog/${id}?reviewsPage=2`);});
it('validates review page and provides recovery',()=>{route.query='reviewsPage=0';setup(<PublicReviews mediaId={id}/>);expect(screen.getByRole('alert')).toHaveTextContent('Invalid review page');expect(mock).not.toHaveBeenCalled();});
it('filters library explicitly with page reset and preserves paging',async()=>{route.query='status=completed&type=movie&page=2';setup(<LibraryScreen/>);expect(await screen.findByRole('link',{name:'A film'})).toHaveAttribute('href','/catalog/'+id);selectValue(screen.getByLabelText('Status'), 'planned');fireEvent.click(screen.getByRole('button',{name:'Apply filters'}));expect(route.push).toHaveBeenCalledWith('/library?status=planned&type=movie');expect(screen.getByRole('link',{name:'Previous'})).toHaveAttribute('href','/library?status=completed&type=movie');});
it('offers login for private library without fetching it',async()=>{session={...session,account:null};setup(<LibraryScreen/>);expect(await screen.findByRole('link',{name:'Log in to view your library'})).toHaveAttribute('href','/login?returnTo=%2Flibrary');expect(mock).not.toHaveBeenCalled();});
it('recovers empty library',async()=>{mock.mockResolvedValue(Response.json({items:[],page:1,pageSize:20,totalCount:0}));setup(<LibraryScreen/>);expect(await screen.findByText('Your library starts here.')).toBeInTheDocument();expect(screen.getByRole('link',{name:'Browse the catalog'})).toHaveAttribute('href','/catalog');});
it('preserves a conflicting review draft until explicitly loading the saved review',async()=>{
 let ownReads=0;const original=mock.getMockImplementation()!;
 mock.mockImplementation((url,opts)=>{
  if(String(url).endsWith('/review'))return Promise.resolve(++ownReads===1?new Response(null,{status:404}):Response.json({...review,body:'Saved elsewhere'}));
  if(String(url).endsWith('/reviews')&&opts?.method==='POST')return Promise.resolve(Response.json({}, {status:409}));
  return original(url,opts);
 });
 setup(<PersonalControls mediaId={id}/>);fireEvent.change(await screen.findByLabelText('Your public review'),{target:{value:'Keep this draft'}});fireEvent.click(screen.getByRole('button',{name:'Publish review'}));await screen.findByRole('button',{name:'Load saved review'});expect(screen.getByLabelText('Your public review')).toHaveValue('Keep this draft');expect(screen.getByRole('button',{name:'Publish review'})).toBeDisabled();fireEvent.click(screen.getByRole('button',{name:'Load saved review'}));expect(screen.getByLabelText('Your public review')).toHaveValue('Saved elsewhere');expect(screen.getByRole('button',{name:'Save review'})).toBeEnabled();
});
it('keeps a review draft after a failed save and requires explicit retry',async()=>{
 const original=mock.getMockImplementation()!;mock.mockImplementation((url,opts)=>opts?.method==='PATCH'?Promise.resolve(Response.json({}, {status:503})):original(url,opts));setup(<PersonalControls mediaId={id}/>);fireEvent.change(await screen.findByLabelText('Your public review'),{target:{value:'My draft'}});fireEvent.click(screen.getByRole('button',{name:'Save review'}));expect(await screen.findByRole('alert')).toHaveTextContent('couldn’t save');expect(screen.getByLabelText('Your public review')).toHaveValue('My draft');expect(mock.mock.calls.filter(([,o])=>o?.method==='PATCH')).toHaveLength(1);
});
it('hides all private forms on401 and refreshes the session',async()=>{
 mock.mockImplementation(async()=>Response.json({}, {status:401}));setup(<PersonalControls mediaId={id}/>);expect(await screen.findByRole('link',{name:'Log in to continue'})).toBeInTheDocument();expect(screen.queryByLabelText('Your public review')).not.toBeInTheDocument();expect(session.refresh).toHaveBeenCalledTimes(1);
});
it('does not send duplicate pending writes',async()=>{
 let resolve!:(r:Response)=>void;const original=mock.getMockImplementation()!;mock.mockImplementation((url,opts)=>opts?.method==='PUT'?new Promise(r=>{resolve=r;}):original(url,opts));setup(<PersonalControls mediaId={id}/>);const form=await screen.findByRole('form',{name:'Rating'});fireEvent.submit(form);fireEvent.submit(form);await waitFor(()=>expect(resolve).toBeTypeOf('function'));expect(mock.mock.calls.filter(([,o])=>o?.method==='PUT')).toHaveLength(1);await act(async()=>resolve(Response.json({score:0})));await screen.findByText('Rating saved.');
});
it('explains a whitespace-only review without sending a write',async()=>{setup(<PersonalControls mediaId={id}/>);fireEvent.change(await screen.findByLabelText('Your public review'),{target:{value:'   '}});fireEvent.submit(screen.getByRole('form',{name:'Review'}));expect(await screen.findByRole('alert')).toHaveTextContent('Write a review before saving.');expect(mock.mock.calls.some(([,o])=>o?.method==='PATCH')).toBe(false);});
it('preserves all drafts across same-account background session checks',async()=>{
 const view=setup(<PersonalControls mediaId={id}/>);fireEvent.change(await screen.findByLabelText('Your public review'),{target:{value:'Unpublished owner draft'}});selectValue(screen.getByLabelText('Tracking status'), 'paused');selectValue(screen.getByLabelText('Your score'), '4.5');
 session={...session,account:undefined};view.rerender(<PersonalControls mediaId={id}/>);
 expect(screen.queryByRole('form',{name:'Review'})).not.toBeInTheDocument();
 session={...session,account};view.rerender(<PersonalControls mediaId={id}/>);
 expect(await screen.findByLabelText('Your public review')).toHaveValue('Unpublished owner draft');expect(screen.getByLabelText('Tracking status')).toHaveTextContent('Paused');expect(screen.getByLabelText('Your score')).toHaveTextContent('4.5 / 5');
});
it('discards hidden drafts on confirmed account change and logout',async()=>{
 const view=setup(<PersonalControls mediaId={id}/>);fireEvent.change(await screen.findByLabelText('Your public review'),{target:{value:'Unpublished owner draft'}});
 session={...session,account:undefined};view.rerender(<PersonalControls mediaId={id}/>);session={...session,account:{...account,id:'other'}};view.rerender(<PersonalControls mediaId={id}/>);expect(await screen.findByLabelText('Your public review')).toHaveValue(review.body);
 fireEvent.change(screen.getByLabelText('Your public review'),{target:{value:'Other draft'}});session={...session,account:null};view.rerender(<PersonalControls mediaId={id}/>);expect(screen.queryByRole('form',{name:'Review'})).not.toBeInTheDocument();session={...session,account};view.rerender(<PersonalControls mediaId={id}/>);expect(await screen.findByLabelText('Your public review')).toHaveValue(review.body);
});
it('cancels a pending write during session checking without losing its draft or replaying it',async()=>{
 let resolve!:(r:Response)=>void;let signal:AbortSignal|undefined;const original=mock.getMockImplementation()!;
 mock.mockImplementation((url,opts)=>opts?.method==='PATCH'?new Promise(r=>{signal=opts.signal??undefined;resolve=r;}):original(url,opts));
 const view=setup(<PersonalControls mediaId={id}/>);fireEvent.change(await screen.findByLabelText('Your public review'),{target:{value:'Keep this pending draft'}});fireEvent.click(screen.getByRole('button',{name:'Save review'}));await waitFor(()=>expect(resolve).toBeTypeOf('function'));
 session={...session,account:undefined};view.rerender(<PersonalControls mediaId={id}/>);expect(signal?.aborted).toBe(true);
 session={...session,account};view.rerender(<PersonalControls mediaId={id}/>);await act(async()=>resolve(Response.json({...review,body:'Late result'})));expect(screen.getByLabelText('Your public review')).toHaveValue('Keep this pending draft');expect(screen.queryByText('Review saved.')).not.toBeInTheDocument();expect(screen.getByRole('button',{name:'Save review'})).toBeEnabled();expect(mock.mock.calls.filter(([,o])=>o?.method==='PATCH')).toHaveLength(1);
});
