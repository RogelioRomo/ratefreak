// @vitest-environment jsdom
import { selectValue } from "@/test/select-value";
import { act, fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { CreateCollectionScreen } from "./create-collection-screen";
import { account, details, mount, sessionValue } from "./test-support";
import { resetCsrfState } from "@/shared/api/csrf";
const route=vi.hoisted(()=>({push:vi.fn()}));
vi.mock('next/navigation',()=>({useRouter:()=>({push:route.push}),usePathname:()=>'/collections/new'}));
const mock=vi.fn<typeof fetch>();
beforeEach(()=>{resetCsrfState();route.push.mockReset();mock.mockReset();mock.mockImplementation(async url=>Response.json(String(url).endsWith('csrf')?{requestToken:'token'}:details,{status:String(url).endsWith('csrf')?200:201}));vi.stubGlobal('fetch',mock);});
async function fill(name=' Cinema '){fireEvent.change(await screen.findByLabelText('Name'),{target:{value:name}});}
it('creates privately with trimmed metadata, empty description null, no fields and navigates only after success',async()=>{
 mount(<CreateCollectionScreen/>);await fill();expect(screen.getByLabelText('Visibility')).toHaveTextContent('Private');fireEvent.click(screen.getByRole('button',{name:'Create collection'}));await waitFor(()=>expect(route.push).toHaveBeenCalledWith(`/collections/${details.collection.id}`));
 const write=mock.mock.calls.find(([,o])=>o?.method==='POST');expect(JSON.parse(String(write?.[1]?.body))).toEqual({name:'Cinema',description:null,visibility:'private'});
});
it('requires login before creation',async()=>{mount(<CreateCollectionScreen/>,{...sessionValue(),account:null});expect(await screen.findByRole('link',{name:'Log in to continue'})).toHaveAttribute('href','/login?returnTo=%2Fcollections%2Fnew');expect(mock).not.toHaveBeenCalled();});
it('validates whitespace and lengths without a write',async()=>{mount(<CreateCollectionScreen/>);await fill('  ');fireEvent.submit(screen.getByRole('form',{name:'Create collection'}));expect(await screen.findByText(/Name must contain/)).toBeVisible();await fill('x'.repeat(121));fireEvent.change(screen.getByLabelText('Description'),{target:{value:'x'.repeat(2001)}});fireEvent.submit(screen.getByRole('form',{name:'Create collection'}));expect(await screen.findByText(/Description must contain/)).toBeVisible();expect(mock).not.toHaveBeenCalled();});
it('preserves draft and displays field errors and public exposure guidance',async()=>{
 mock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):Response.json({errors:{name:['Rejected name']}},{status:400}));mount(<CreateCollectionScreen/>);await fill('Public notes');selectValue(screen.getByLabelText('Visibility'), 'public');expect(screen.getByText(/Public shares this list/)).toBeVisible();fireEvent.click(screen.getByRole('button',{name:'Create collection'}));expect(await screen.findByText('Rejected name')).toBeVisible();expect(screen.getByLabelText('Name')).toHaveValue('Public notes');expect(route.push).not.toHaveBeenCalled();
});
it('suppresses duplicate writes and preserves the draft on 429',async()=>{
 let resolve!:(r:Response)=>void;mock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):new Promise(r=>{resolve=r;}));mount(<CreateCollectionScreen/>);await fill();const form=screen.getByRole('form',{name:'Create collection'});fireEvent.submit(form);fireEvent.submit(form);await waitFor(()=>expect(resolve).toBeTypeOf('function'));expect(mock.mock.calls.filter(([,o])=>o?.method==='POST')).toHaveLength(1);await act(async()=>resolve(Response.json({}, {status:429})));expect(await screen.findByRole('alert')).toHaveTextContent(/limited/i);expect(screen.getByLabelText('Name')).toHaveValue(' Cinema ');
});
it('cancels during a session check, restores same-account draft, and explains uncertain create without replay',async()=>{
 let resolve!:(r:Response)=>void;let signal:AbortSignal|undefined;mock.mockImplementation(async (url,opts)=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):new Promise(r=>{resolve=r;signal=opts?.signal??undefined;}));const s=sessionValue();const view=mount(<CreateCollectionScreen/>,s);await fill('Pending notes');fireEvent.click(screen.getByRole('button',{name:'Create collection'}));await waitFor(()=>expect(resolve).toBeTypeOf('function'));view.change({...s,account:undefined});expect(signal?.aborted).toBe(true);view.change(s);await act(async()=>resolve(Response.json(details)));expect(await screen.findByText(/outcome is unknown/)).toBeVisible();expect(screen.getByLabelText('Name')).toHaveValue('Pending notes');expect(screen.getByRole('link',{name:'Check My collections'})).toHaveAttribute('href','/collections?scope=mine');expect(route.push).not.toHaveBeenCalled();expect(mock.mock.calls.filter(([,o])=>o?.method==='POST')).toHaveLength(1);
});
it('ignores a late create after account replacement',async()=>{
 let resolve!:(r:Response)=>void;mock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):new Promise(r=>{resolve=r;}));const s=sessionValue();const view=mount(<CreateCollectionScreen/>,s);await fill('Old owner draft');fireEvent.click(screen.getByRole('button',{name:'Create collection'}));await waitFor(()=>expect(resolve).toBeTypeOf('function'));view.change({...s,account:{...account,id:'other'}});await act(async()=>resolve(Response.json(details)));expect(await screen.findByLabelText('Name')).toHaveValue('');expect(route.push).not.toHaveBeenCalled();
});
