// @vitest-environment jsdom
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClientProvider, focusManager } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { createQueryClient } from "@/shared/query/create-query-client";
import { resetCsrfState } from "@/shared/api/csrf";
import { InstallationBoundary } from "./installation-boundary";
const nav=vi.hoisted(()=>({replace:vi.fn(),pathname:"/"}));
vi.mock("next/navigation",()=>({useRouter:()=>nav,usePathname:()=>nav.pathname}));
let required=true,configured=true,statusFailure=false;
let submitStatus=201;
const code="12345678-90ABCDEF-12345678-90ABCDEF";
const fetchMock=vi.fn<typeof fetch>();
beforeEach(()=>{
  required=true;configured=true;statusFailure=false;submitStatus=201;nav.pathname="/";nav.replace.mockReset();resetCsrfState();fetchMock.mockReset();
  fetchMock.mockImplementation(async url=>{
    if(String(url).endsWith("setup/status"))return statusFailure?new Response(null,{status:503}):Response.json({required,configured});
    if(String(url).endsWith("auth/csrf"))return Response.json({requestToken:"token"});
    if(String(url).endsWith("/setup")){
      if(submitStatus===201){required=false;return Response.json({id:"admin",handle:"owner",displayName:"Owner",email:"owner@example.test",createdAt:"2026-11-06T00:00:00Z",bio:null,recoveryCode:code},{status:201});}
      if(submitStatus===409)required=false;
      return Response.json({title:"Setup failed."},{status:submitStatus});
    }
    throw new Error("Unexpected request");
  });vi.stubGlobal("fetch",fetchMock);
});
function mount(){const client=createQueryClient();return {client,...render(<QueryClientProvider client={client}><InstallationBoundary><p>Normal application</p></InstallationBoundary></QueryClientProvider>)};}
async function fill(){
  fireEvent.change(await screen.findByLabelText("Deployment secret"),{target:{value:"a".repeat(64)}});
  fireEvent.change(screen.getByLabelText("Display name"),{target:{value:"Owner"}});
  fireEvent.change(screen.getByLabelText("Handle"),{target:{value:"owner"}});
  fireEvent.change(screen.getByLabelText("Email"),{target:{value:"owner@example.test"}});
  fireEvent.change(screen.getByLabelText("Password"),{target:{value:"Initial-admin-password-72"}});
}
it("blocks normal content and navigation while setup is pending",async()=>{
  mount();expect(screen.queryByText("Normal application")).not.toBeInTheDocument();
  expect(await screen.findByLabelText("Deployment secret")).toHaveAttribute("type","password");
  expect(screen.queryByText("Normal application")).not.toBeInTheDocument();
  expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  await waitFor(()=>expect(nav.replace).toHaveBeenCalledWith("/setup"));
});
it("renders the normal app after confirmed completion",async()=>{required=false;mount();expect(await screen.findByText("Normal application")).toBeVisible();expect(screen.queryByLabelText("Deployment secret")).not.toBeInTheDocument();});
it("redirects a completed installation away from setup",async()=>{required=false;nav.pathname="/setup";mount();await waitFor(()=>expect(nav.replace).toHaveBeenCalledWith("/"));});
it("fails closed with retry on status errors",async()=>{statusFailure=true;mount();expect(await screen.findByRole("alert")).toHaveTextContent(/couldn’t check installation/i);expect(screen.queryByText("Normal application")).not.toBeInTheDocument();statusFailure=false;fireEvent.click(screen.getByRole("button",{name:"Retry setup check"}));expect(await screen.findByLabelText("Deployment secret")).toBeVisible();});
it("requires operator configuration before offering the form",async()=>{configured=false;mount();expect(await screen.findByText(/site owner.*deployment secret/i)).toBeVisible();expect(screen.queryByLabelText("Deployment secret")).not.toBeInTheDocument();});
it("preserves the recovery save step when status changes and keeps submitted secrets out of caches",async()=>{
  const {client}=mount();await fill();fireEvent.submit(screen.getByRole("form",{name:"Create initial administrator"}));
  expect(await screen.findByText(code)).toBeVisible();
  expect(JSON.stringify(client.getMutationCache().getAll().map(m=>m.state))).not.toMatch(/12345678|aaaaaaaaaaaaaaaa|Initial-admin-password/);
  act(()=>{focusManager.setFocused(false);focusManager.setFocused(true);});
  await waitFor(()=>expect(fetchMock.mock.calls.filter(([u])=>String(u).endsWith("setup/status")).length).toBeGreaterThan(1));
  expect(screen.getByText(code)).toBeVisible();expect(screen.queryByText("Normal application")).not.toBeInTheDocument();
  fireEvent.click(screen.getByLabelText("I saved my recovery code"));fireEvent.click(screen.getByRole("button",{name:"Continue to login"}));
  await waitFor(()=>expect(nav.replace).toHaveBeenCalledWith("/login"));
  expect(screen.queryByText(code)).not.toBeInTheDocument();focusManager.setFocused(undefined);
});
it("clears secret/password after invalid setup and allows deliberate retry",async()=>{
  submitStatus=400;mount();await fill();fireEvent.submit(screen.getByRole("form",{name:"Create initial administrator"}));
  await screen.findByRole("alert");expect(screen.getByLabelText("Deployment secret")).toHaveValue("");expect(screen.getByLabelText("Password")).toHaveValue("");expect(screen.getByLabelText("Handle")).toHaveValue("owner");
});
it("refreshes a stale tab when another person completed setup",async()=>{
  submitStatus=409;mount();await fill();fireEvent.submit(screen.getByRole("form",{name:"Create initial administrator"}));
  expect(await screen.findByText("Normal application")).toBeVisible();
});

it("fails closed if a setup status response has no valid booleans",async()=>{
  const original=fetchMock.getMockImplementation()!;
  fetchMock.mockImplementation((url,options)=>String(url).endsWith("setup/status")?Promise.resolve(Response.json({})):original(url,options));
  mount();expect(await screen.findByRole("alert")).toHaveTextContent(/couldn’t check installation/i);
  expect(screen.queryByText("Normal application")).not.toBeInTheDocument();
});

it("keeps normal application drafts mounted across a status failure on tab focus",async()=>{
  required=false;
  const client=createQueryClient();
  render(<QueryClientProvider client={client}><InstallationBoundary><label>Draft<input aria-label="Draft" defaultValue="" /></label></InstallationBoundary></QueryClientProvider>);
  const input=await screen.findByLabelText("Draft");fireEvent.change(input,{target:{value:"Keep this unsaved review"}});
  statusFailure=true;
  await act(async()=>{focusManager.setFocused(false);focusManager.setFocused(true);await new Promise(resolve=>setTimeout(resolve,30));});
  expect(screen.getByLabelText("Draft")).toBe(input);expect(input).toHaveValue("Keep this unsaved review");
  focusManager.setFocused(undefined);
});
