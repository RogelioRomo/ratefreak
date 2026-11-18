// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import { SessionContext } from "@/features/auth/session/session-context";
import { NotificationReadProvider } from "../hooks/use-open-notification";
import { NotificationsScreen } from "./notifications-screen";
import { NotificationLink } from "./notification-link";
const api=vi.hoisted(()=>({getNotifications:vi.fn(),getUnreadCount:vi.fn(),setNotificationRead:vi.fn(),deleteNotification:vi.fn(),clearNotifications:vi.fn(),readAllNotifications:vi.fn()}));
vi.mock("../api/notifications",()=>api);
const item={id:"n1",kind:"follow" as const,actor:{id:"actor",handle:"reader",displayName:"Reader"},commentId:null,reviewId:null,mediaItemId:null,createdAt:"2026-11-18T12:00:00Z",readAt:null};
const account={id:"owner",handle:"owner",displayName:"Owner",email:"owner@example.test",createdAt:"2026-01-01"};
function setup(signedIn=true, totalCount=1){
 const client=new QueryClient({defaultOptions:{queries:{retry:false},mutations:{retry:false}}});
 api.getNotifications.mockResolvedValue({items:[item],page:1,pageSize:20,totalCount,unreadCount:1}); api.getUnreadCount.mockResolvedValue({unreadCount:1});
 const session={account:signedIn?account:null,error:null,isLoading:false,isTransitioning:false,refresh:async()=>{},runExclusive:async<T,>(fn:()=>Promise<T>)=>fn(),changeIdentity:async()=>{}};
 const view=render(<QueryClientProvider client={client}><SessionContext.Provider value={session}><NotificationReadProvider><NotificationLink/><NotificationsScreen/></NotificationReadProvider></SessionContext.Provider></QueryClientProvider>);
 return {client,view,session};
}
it("shows unread events and marks read only on explicit action",async()=>{
 setup(); expect(await screen.findByRole("link",{name:"Reader started following you."})).toHaveAttribute("href","/users/reader");
 expect(api.setNotificationRead).not.toHaveBeenCalled(); fireEvent.click(screen.getByRole("button",{name:"Mark as read"}));
 await waitFor(()=>expect(api.setNotificationRead).toHaveBeenCalledWith("n1",true,expect.any(AbortSignal)));
});
it("deletes only the selected entry and refreshes unread state",async()=>{
 api.deleteNotification.mockResolvedValue(undefined); setup(); fireEvent.click(await screen.findByRole("button",{name:"Delete notification"}));
 await waitFor(()=>expect(api.deleteNotification).toHaveBeenCalledWith("n1",expect.any(AbortSignal))); expect(await screen.findByRole("status")).toHaveTextContent("Notification deleted.");
});
it("confirms clearing, allows cancellation and restores trigger focus",async()=>{
 setup(); const clear=await screen.findByRole("button",{name:"Clear inbox"}); clear.focus(); fireEvent.click(clear);
 expect(screen.getByRole("dialog")).toHaveTextContent("including temporarily hidden notifications");
 const cancel=screen.getByRole("button",{name:"Cancel"}); expect(cancel).toHaveFocus(); fireEvent.click(cancel);
 expect(screen.queryByRole("dialog")).not.toBeInTheDocument(); expect(clear).toHaveFocus(); expect(api.clearNotifications).not.toHaveBeenCalled();
 fireEvent.click(clear); fireEvent.click(screen.getByRole("button",{name:"Permanently clear inbox"}));
 await waitFor(()=>expect(api.clearNotifications).toHaveBeenCalledWith(expect.any(AbortSignal)));
});
it("signed out users see no private entries or badge and do not fetch",()=>{
 setup(false); expect(screen.getByRole("link",{name:"Log in to view notifications"})).toHaveAttribute("href","/login?returnTo=%2Fnotifications");
 expect(api.getNotifications).not.toHaveBeenCalled(); expect(api.getUnreadCount).not.toHaveBeenCalled();
});
it("shows a recoverable mutation error",async()=>{
 api.deleteNotification.mockRejectedValueOnce(new Error("offline")); setup(); fireEvent.click(await screen.findByRole("button",{name:"Delete notification"}));
 expect(await screen.findByRole("alert")).toHaveTextContent("couldn’t confirm"); expect(screen.getByRole("link",{name:"Reader started following you."})).toBeVisible();
});

it("moves back to a valid page after deleting its final entry",async()=>{
 setup(true,21); await screen.findByRole("link",{name:"Reader started following you."});
 let deleted=false;
 api.getNotifications.mockImplementation(async(page:number)=>({items:page===2?(deleted?[]:[{...item,id:"last"}]):[item],page,pageSize:20,totalCount:deleted?20:21,unreadCount:1}));
 api.deleteNotification.mockImplementation(async()=>{deleted=true;});
 fireEvent.click(screen.getByRole("button",{name:"Next"}));
 await waitFor(()=>expect(screen.getByText("Page 2 of 2")).toBeVisible());
 fireEvent.click(screen.getByRole("button",{name:"Delete notification"}));
 await waitFor(()=>expect(api.getNotifications).toHaveBeenLastCalledWith(1,expect.any(AbortSignal)));
 expect(await screen.findByRole("link",{name:"Reader started following you."})).toBeVisible();
});
it("conceals old inbox and count during an account transition",async()=>{
 const {view,client,session}=setup(); await screen.findByRole("link",{name:"Reader started following you."});
 view.rerender(<QueryClientProvider client={client}><SessionContext.Provider value={{...session,account:undefined,isTransitioning:true}}><NotificationReadProvider><NotificationLink/><NotificationsScreen/></NotificationReadProvider></SessionContext.Provider></QueryClientProvider>);
 expect(screen.queryByRole("link",{name:"Reader started following you."})).not.toBeInTheDocument();
 expect(screen.queryByRole("link",{name:/Notifications, /})).not.toBeInTheDocument();
});

it("lets link-triggered read requests survive route navigation",async()=>{
 const {view,client,session}=setup(); const link=await screen.findByRole("link",{name:"Reader started following you."});
 let finish!:()=>void; let signal!:AbortSignal;
 api.setNotificationRead.mockImplementation((_id:string,_read:boolean,s:AbortSignal)=>{signal=s;return new Promise<void>(resolve=>{finish=resolve;});});
 link.addEventListener("click",e=>e.preventDefault()); fireEvent.click(link);
 await waitFor(()=>expect(finish).toBeTypeOf("function"));
 view.rerender(<QueryClientProvider client={client}><SessionContext.Provider value={session}><NotificationReadProvider><p>Destination page</p></NotificationReadProvider></SessionContext.Provider></QueryClientProvider>);
 expect(signal.aborted).toBe(false); finish();
});
