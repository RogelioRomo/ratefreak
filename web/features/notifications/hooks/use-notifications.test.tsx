// @vitest-environment jsdom
import { act, render, waitFor } from "@testing-library/react";
import { focusManager, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import { SessionContext } from "@/features/auth/session/session-context";
import { NotificationSync, useNotificationCount } from "./use-notifications";
import { getUnreadCount } from "../api/notifications";
vi.mock("../api/notifications",()=>({getUnreadCount:vi.fn(async()=>({unreadCount:2}))}));
function Observer(){useNotificationCount();return null;}
it("shares one poller, pauses in the background and always refreshes on focus",async()=>{
 const client=new QueryClient({defaultOptions:{queries:{retry:false}}});
 const value={account:{id:"a",handle:"reader",displayName:"Reader",email:"reader@example.test",createdAt:"2026-11-18"},error:null,isLoading:false,isTransitioning:false,refresh:async()=>{},runExclusive:async<T,>(fn:()=>Promise<T>)=>fn(),changeIdentity:async()=>{}};
 const view=render(<QueryClientProvider client={client}><SessionContext.Provider value={value}><NotificationSync/><Observer/><Observer/></SessionContext.Provider></QueryClientProvider>);
 try{
  await waitFor(()=>expect(getUnreadCount).toHaveBeenCalledTimes(1));
  act(()=>focusManager.setFocused(false));vi.useFakeTimers();
  await act(async()=>{await vi.advanceTimersByTimeAsync(60_000);});expect(getUnreadCount).toHaveBeenCalledTimes(1);
  await act(async()=>{focusManager.setFocused(true);await vi.advanceTimersByTimeAsync(10);});expect(getUnreadCount).toHaveBeenCalledTimes(2);
  // This second focus occurs while the just-fetched result is still fresh.
  await act(async()=>{focusManager.setFocused(false);focusManager.setFocused(true);await vi.advanceTimersByTimeAsync(10);});expect(getUnreadCount).toHaveBeenCalledTimes(3);
 }finally{view.unmount();client.clear();focusManager.setFocused(undefined);vi.useRealTimers();}
});
