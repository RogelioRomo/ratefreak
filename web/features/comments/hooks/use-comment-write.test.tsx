// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import { useCommentWrite } from "./use-comment-write";
import { ApiError } from "@/shared/api/api-error";
const session=vi.hoisted(()=>({account:{id:'owner'} as {id:string}|undefined,isLoading:false,isTransitioning:false,error:null,refresh:vi.fn()}));
vi.mock('@/features/auth/hooks/use-session',()=>({useSession:()=>session}));
function setup(){return renderHook(()=>useCommentWrite(),{wrapper:({children}:{children:ReactNode})=><QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>});}
it('returns known saves separately from validation and transport uncertainty',async()=>{const hook=setup();let result:unknown;await act(async()=>{result=await hook.result.current.run(async()=>42);});expect(result).toEqual({kind:'saved',value:42});await act(async()=>{result=await hook.result.current.run(async()=>{throw new ApiError(400);});});expect(result).toMatchObject({kind:'rejected'});await act(async()=>{result=await hook.result.current.run(async()=>{throw new TypeError('offline');});});expect(result).toEqual({kind:'uncertain'});});
it('aborts on session uncertainty and rejects a late completion',async()=>{const hook=setup();let finish!:(v:string)=>void;let signal!:AbortSignal;let pending!:Promise<unknown>;act(()=>{pending=hook.result.current.run(s=>{signal=s;return new Promise(resolve=>{finish=resolve;});});});session.account=undefined;hook.rerender();expect(signal.aborted).toBe(true);await act(async()=>finish('saved'));expect(await pending).toEqual({kind:'uncertain'});session.account={id:'owner'};});
