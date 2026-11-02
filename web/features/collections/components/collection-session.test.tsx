// @vitest-environment jsdom
import { act, fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { CollectionSession } from "./collection-session";
import { useCollections } from "../hooks/use-collections";
import { account, details, mount, sessionValue } from "./test-support";
const mock=vi.fn<typeof fetch>();
function Content(){return <CollectionSession returnTo="/collections">{state=><Reader {...state}/>}</CollectionSession>;}
function Reader({viewer,active}:{viewer:string;active:boolean}){const query=useCollections(viewer,{scope:'all',page:1,pageSize:20},active);return <><input aria-label="Draft" defaultValue="Original"/><p>{query.data?.items[0]?.name}</p></>;}
beforeEach(()=>{mock.mockReset();mock.mockResolvedValue(Response.json({items:[{...details.collection,name:'Secret collection'}],page:1,pageSize:20,totalCount:1}));vi.stubGlobal('fetch',mock);});
it('does not read before session discovery and allows verified anonymous browsing',async()=>{
 const s=sessionValue();const view=mount(<Content/>,{...s,account:undefined,isLoading:true});expect(mock).not.toHaveBeenCalled();expect(screen.queryByLabelText('Draft')).toBeNull();
 view.change({...s,account:null});expect(await screen.findByText('Secret collection')).toBeVisible();expect(mock).toHaveBeenCalledTimes(1);
});
it('retains same-account drafts concealed and inert through checks and session failures',async()=>{
 const s=sessionValue();const view=mount(<Content/>,s);const input=await screen.findByLabelText('Draft');fireEvent.change(input,{target:{value:'Unsaved'}});
 view.change({...s,account:undefined});expect(input).not.toBeVisible();expect(input).toBeDisabled();expect(input.closest('[inert]')).not.toBeNull();
 view.change({...s,account:undefined,error:new Error('Offline')});expect(screen.getByRole('button',{name:'Retry session'})).toBeVisible();expect(input).not.toBeVisible();
 view.change(s);expect(screen.getByLabelText('Draft')).toBe(input);expect(input).toHaveValue('Unsaved');
});
it('discards retained drafts on confirmed account changes and logout',async()=>{
 const s=sessionValue();const view=mount(<Content/>,s);fireEvent.change(await screen.findByLabelText('Draft'),{target:{value:'Secret draft'}});
 view.change({...s,account:undefined});view.change({...s,account:{...account,id:'other'}});expect(await screen.findByLabelText('Draft')).toHaveValue('Original');
 fireEvent.change(screen.getByLabelText('Draft'),{target:{value:'Other draft'}});view.change({...s,account:null});expect(await screen.findByLabelText('Draft')).toHaveValue('Original');
});
it('offers login for account-only pages without querying and explicit session recovery',async()=>{
 const s=sessionValue();const view=mount(<CollectionSession requireAccount returnTo="/collections/new">{()=> <p>Private</p>}</CollectionSession>,{...s,account:null});
 expect(await screen.findByRole('link',{name:'Log in to continue'})).toHaveAttribute('href','/login?returnTo=%2Fcollections%2Fnew');expect(mock).not.toHaveBeenCalled();
 view.change({...s,account:undefined,error:new Error('Offline')});fireEvent.click(screen.getByRole('button',{name:'Retry session'}));await waitFor(()=>expect(s.refresh).toHaveBeenCalled());expect(screen.queryByText('Private')).toBeNull();
});
it('aborts a viewer read when identity checking starts',async()=>{
 let signal:AbortSignal|undefined;mock.mockImplementation((_url,opts)=>{signal=opts?.signal??undefined;return new Promise(()=>{});});
 const s=sessionValue();const view=mount(<Content/>,s);await waitFor(()=>expect(signal).toBeDefined());view.change({...s,account:undefined});await act(async()=>{});expect(signal?.aborted).toBe(true);
});
