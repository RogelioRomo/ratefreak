// @vitest-environment jsdom
import {act,fireEvent,render,screen,waitFor} from "@testing-library/react";
import {QueryClient,QueryClientProvider} from "@tanstack/react-query";
import {beforeEach,expect,it,vi} from "vitest";
import {SessionContext} from "@/features/auth/session/session-context";
import type {SessionContextValue} from "@/features/auth/interfaces/session-context-value";
import {resetCsrfState} from "@/shared/api/csrf";
import {selectValue} from "@/test/select-value";
import {LibraryScreen} from "./library-screen";
const route=vi.hoisted(()=>({query:"",push:vi.fn(),replace:vi.fn()}));
vi.mock("next/navigation",()=>({useSearchParams:()=>new URLSearchParams(route.query),useRouter:()=>({push:route.push,replace:route.replace})}));
const id="a1111111-1111-1111-1111-111111111111",accountId="b1111111-1111-1111-1111-111111111111";
const account={id:accountId,handle:"reader",displayName:"Reader",email:"reader@example.test",createdAt:"2026-01-01"};
const entry={media:{id,type:"movie",title:"A film",releaseDate:null,imageReference:null},status:"planned",startedAt:"2026-01-01",completedAt:null,createdAt:"2026-01-01",updatedAt:"2026-01-01",ownRating:0};
let session:SessionContextValue;const mock=vi.fn<typeof fetch>();
function setup(){const client=new QueryClient({defaultOptions:{queries:{retry:false,gcTime:0}}});const wrapper=({children}:{children:React.ReactNode})=><QueryClientProvider client={client}><SessionContext.Provider value={session}>{children}</SessionContext.Provider></QueryClientProvider>;return {...render(<LibraryScreen/>,{wrapper}),client};}
beforeEach(()=>{route.query="";route.push.mockReset();route.replace.mockReset();resetCsrfState();session={account,isLoading:false,isTransitioning:false,error:null,refresh:vi.fn().mockResolvedValue(undefined),runExclusive:vi.fn(),changeIdentity:vi.fn()};mock.mockReset();vi.stubGlobal("fetch",mock);mock.mockImplementation(async(url,opts)=>String(url).endsWith("csrf")?Response.json({requestToken:"token"}):opts?.method==="PATCH"?Response.json({...entry,status:"completed",updatedAt:"2026-02-01"}):Response.json({items:[entry],page:1,pageSize:20,totalCount:1}));});
it("submits search and ordering together with filters and resets paging",async()=>{
 route.query="status=planned&page=2";setup();fireEvent.change(await screen.findByLabelText("Search your library"),{target:{value:" film "}});selectValue(screen.getByLabelText("Sort by"),"highest_rated");fireEvent.submit(screen.getByRole("form",{name:"Library filters"}));expect(route.push).toHaveBeenCalledWith("/library?status=planned&q=film&sort=highest_rated");
});
it("restores search and sort fields on browser navigation",async()=>{
 route.query="q=first&sort=title";const view=setup();expect(await screen.findByLabelText("Search your library")).toHaveValue("first");route.query="q=second&sort=lowest_rated";view.rerender(<LibraryScreen/>);expect(screen.getByLabelText("Search your library")).toHaveValue("second");expect(screen.getByLabelText("Sort by")).toHaveTextContent("Lowest rated");
});
it("displays zero as a rating and distinguishes an unrated entry",async()=>{
 const view=setup();expect(await screen.findByText("Your rating: 0.0 / 5")).toBeInTheDocument();mock.mockResolvedValue(Response.json({items:[{...entry,ownRating:null}],page:1,pageSize:20,totalCount:1}));await act(()=>view.client.invalidateQueries({queryKey:["personal",accountId,"library"]}));expect(await screen.findByText("Not rated")).toBeInTheDocument();
});
it("saves only status and binds it to the account and previous status",async()=>{
 setup();selectValue(await screen.findByRole("combobox",{name:"Status for A film"}),"completed");fireEvent.click(screen.getByRole("button",{name:"Save status for A film"}));await screen.findByText("Status saved for A film.");const call=mock.mock.calls.find(([,o])=>o?.method==="PATCH")!;expect(call[0]).toBe(`/api/v1/users/me/library/${id}/status`);expect(JSON.parse(String(call[1]?.body))).toEqual({status:"completed",expectedStatus:"planned"});expect(new Headers(call[1]?.headers).get("X-RateFreak-Account-Id")).toBe(accountId);
});
it("keeps failed drafts and offers explicit conflict refresh",async()=>{
 const original=mock.getMockImplementation()!;mock.mockImplementation((u,o)=>o?.method==="PATCH"?Promise.resolve(Response.json({},{status:409})):original(u,o));setup();selectValue(await screen.findByRole("combobox",{name:"Status for A film"}),"paused");fireEvent.click(screen.getByRole("button",{name:"Save status for A film"}));expect(await screen.findByRole("alert")).toHaveTextContent("changed");expect(screen.getByRole("combobox",{name:"Status for A film"})).toHaveTextContent("Paused");expect(screen.getByRole("button",{name:"Refresh library"})).toBeInTheDocument();expect(screen.getByRole("button",{name:"Save status for A film"})).toBeDisabled();
});
it("shows a retryable error without announcing success",async()=>{
 const original=mock.getMockImplementation()!;mock.mockImplementation((u,o)=>o?.method==="PATCH"?Promise.resolve(Response.json({},{status:503})):original(u,o));setup();selectValue(await screen.findByRole("combobox",{name:"Status for A film"}),"paused");fireEvent.click(screen.getByRole("button",{name:"Save status for A film"}));expect(await screen.findByRole("alert")).toHaveTextContent("couldn’t save");expect(screen.queryByText("Status saved for A film.")).toBeNull();expect(screen.getByRole("combobox",{name:"Status for A film"})).toHaveTextContent("Paused");
});
it("announces a filtered removal and returns a later empty page to the last valid page",async()=>{
 route.query="status=planned&page=2";let saved=false;mock.mockImplementation(async(u,o)=>String(u).endsWith("csrf")?Response.json({requestToken:"token"}):o?.method==="PATCH"?(saved=true,Response.json({...entry,status:"completed"})):Response.json({items:saved?[]:[entry],page:2,pageSize:20,totalCount:saved?20:21}));setup();selectValue(await screen.findByRole("combobox",{name:"Status for A film"}),"completed");fireEvent.click(screen.getByRole("button",{name:"Save status for A film"}));await screen.findByText("Status saved for A film.");await waitFor(()=>expect(route.replace).toHaveBeenCalledWith("/library?status=planned",{scroll:false}));expect(screen.queryByRole("button",{name:"Save status for A film"})).toBeNull();
});
it("focuses the results when the saved row disappears on page one",async()=>{
 route.query="status=planned";let saved=false;mock.mockImplementation(async(u,o)=>String(u).endsWith("csrf")?Response.json({requestToken:"token"}):o?.method==="PATCH"?(saved=true,Response.json({...entry,status:"completed"})):Response.json({items:saved?[]:[entry],page:1,pageSize:20,totalCount:saved?0:1}));setup();selectValue(await screen.findByRole("combobox",{name:"Status for A film"}),"completed");const button=screen.getByRole("button",{name:"Save status for A film"});button.focus();fireEvent.click(button);await screen.findByText("Status saved for A film.");await waitFor(()=>expect(screen.getByLabelText("Library results")).toHaveFocus());
});
it("cancels a pending write and conceals rows on account transition",async()=>{
 let resolve!:(r:Response)=>void,signal:AbortSignal|null|undefined;const original=mock.getMockImplementation()!;mock.mockImplementation((u,o)=>o?.method==="PATCH"?new Promise(r=>{resolve=r;signal=o.signal;}):original(u,o));const view=setup();selectValue(await screen.findByRole("combobox",{name:"Status for A film"}),"completed");const form=screen.getByRole("form",{name:"Update status for A film"});fireEvent.submit(form);fireEvent.submit(form);await waitFor(()=>expect(resolve).toBeTypeOf("function"));expect(mock.mock.calls.filter(([,o])=>o?.method==="PATCH")).toHaveLength(1);session={...session,isTransitioning:true};view.rerender(<LibraryScreen/>);expect(screen.queryByText("A film")).toBeNull();expect(signal?.aborted).toBe(true);await act(async()=>resolve(Response.json({...entry,status:"completed"})));expect(screen.queryByText("Status saved for A film.")).toBeNull();
});
it("conceals rows and failed-refresh data after logout or a session error",async()=>{
 const view=setup();await screen.findByText("A film");session={...session,error:new Error("offline")};view.rerender(<LibraryScreen/>);expect(screen.queryByText("A film")).toBeNull();session={...session,error:null,account:null};view.rerender(<LibraryScreen/>);expect(screen.queryByText("A film")).toBeNull();
});
