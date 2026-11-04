// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { Connections } from "./connections";
const state=vi.hoisted(()=>({query:'',account:null as null | {id:string},error:null,isTransitioning:false}));
vi.mock('next/navigation',()=>({useSearchParams:()=>new URLSearchParams(state.query)}));
vi.mock('@/features/auth/hooks/use-session',()=>({useSession:()=>state}));
const id='a1111111-1111-1111-1111-111111111111';
beforeEach(()=>{state.query='';state.account=null;vi.stubGlobal('fetch',vi.fn(async()=>Response.json({items:[],totalCount:0,page:1,pageSize:20})));});
function setup(){return render(<QueryClientProvider client={new QueryClient({defaultOptions:{queries:{retry:false}}})}><Connections userId={id} handle="target" /></QueryClientProvider>);}
it('allows anonymous public lists with login continuation and no write controls',async()=>{setup();await screen.findByText('No connections to show yet.');expect(screen.getByRole('link',{name:'Log in to follow @target'})).toHaveAttribute('href','/login?returnTo=%2Fusers%2Ftarget');expect(screen.queryByRole('button',{name:'Follow'})).toBeNull();});
it('invalid filters do not fetch',async()=>{state.query='page=2&page=3';setup();expect(screen.getByRole('alert')).toHaveTextContent('Invalid');expect(fetch).not.toHaveBeenCalled();});
it('renders profile links and bounded paging for returned data',async()=>{state.query='connections=following&page=2';vi.mocked(fetch).mockResolvedValue(Response.json({items:[{id:'other',handle:'other_user',displayName:'Other user',followedAt:'2026-11-01'}],totalCount:41,page:2,pageSize:20}));setup();expect(await screen.findByRole('link',{name:'Other user'})).toHaveAttribute('href','/users/other_user');expect(screen.getByRole('link',{name:'Previous'})).toHaveAttribute('href','/users/target?connections=following');expect(screen.getByRole('link',{name:'Next'})).toHaveAttribute('href','/users/target?connections=following&page=3');expect(screen.getByRole('link',{name:'Followers'})).toHaveAttribute('href','/users/target');});
it('offers first page recovery for out of range lists',async()=>{state.query='page=2';setup();await screen.findByText('No connections on this page.');expect(screen.getByRole('link',{name:'First page'})).toHaveAttribute('href','/users/target');});
