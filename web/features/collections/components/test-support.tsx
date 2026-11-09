import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { vi } from "vitest";
import type { ReactNode } from "react";
import { SessionContext } from "@/features/auth/session/session-context";
import type { SessionContextValue } from "@/features/auth/interfaces/session-context-value";
import type { CollectionDetails } from "../interfaces/collections";
export const id = 'a1111111-1111-1111-1111-111111111111';
export const account = { id:'owner',handle:'reader',displayName:'Reader',email:'reader@example.test',createdAt:'2026-11-01' };
export const details: CollectionDetails = {collection:{id,owner:{id:'owner',handle:'reader',displayName:'Reader'},name:'Cinema',description:'Personal notes',visibility:'private',revision:1,createdAt:'2026-11-01',updatedAt:'2026-11-01'}};
export function sessionValue(): SessionContextValue { return {account,isLoading:false,isTransitioning:false,error:null,refresh:vi.fn(async()=>{}),runExclusive:async fn=>fn(),changeIdentity:vi.fn()}; }
export function mount(node: ReactNode, initial=sessionValue()) {
 const client=new QueryClient({defaultOptions:{queries:{retry:false,gcTime:Infinity},mutations:{retry:false}}});
 let session=initial;
 const wrap=(child:ReactNode)=><QueryClientProvider client={client}><SessionContext.Provider value={session}>{child}</SessionContext.Provider></QueryClientProvider>;
 const view=render(wrap(node));
 return {...view,client,change:(next:SessionContextValue,child:ReactNode=node)=>{session=next;view.rerender(wrap(child));}};
}
