// @vitest-environment jsdom
import { selectValue } from "@/test/select-value";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { CollectionsScreen } from "./collections-screen";
import { details, mount, sessionValue } from "./test-support";
const route=vi.hoisted(()=>({query:'',push:vi.fn()}));
vi.mock('next/navigation',()=>({useSearchParams:()=>new URLSearchParams(route.query),useRouter:()=>({push:route.push})}));
const mock=vi.fn<typeof fetch>();
beforeEach(()=>{route.query='';route.push.mockReset();mock.mockReset();mock.mockImplementation(async()=>Response.json({items:[details.collection],page:1,pageSize:20,totalCount:21}));vi.stubGlobal('fetch',mock);});
it('shows accessible collections, owner and honest private visibility',async()=>{
 mount(<CollectionsScreen/>);expect(await screen.findByRole('link',{name:'Cinema'})).toHaveAttribute('href',`/collections/${details.collection.id}`);expect(screen.getByText(/Reader.*reader/)).toBeVisible();expect(screen.getByText('Private')).toBeVisible();expect(screen.getByText(/your own private collections/)).toBeVisible();expect(screen.getByRole('link',{name:'Next'})).toHaveAttribute('href','/collections?page=2');
});
it('applies explicit scope and resets paging; history rerenders restore controls',async()=>{
 route.query='page=2';const s=sessionValue();const view=mount(<CollectionsScreen/>,s);const scope=await screen.findByLabelText('Show collections');selectValue(scope, 'mine');expect(route.push).not.toHaveBeenCalled();fireEvent.click(screen.getByRole('button',{name:'Apply'}));expect(route.push).toHaveBeenCalledWith('/collections?scope=mine');
 route.query='scope=mine&page=3';view.change(s,<CollectionsScreen/>);expect(await screen.findByLabelText('Show collections')).toHaveTextContent('My collections');await waitFor(()=>expect(mock.mock.calls.some(([url])=>String(url).endsWith('ownOnly=true&page=3&pageSize=20'))).toBe(true));
 route.query='page=2';view.change(s,<CollectionsScreen/>);expect(await screen.findByLabelText('Show collections')).toHaveTextContent('All accessible');
});
it('rejects invalid filters without reading',async()=>{route.query='page=0';mount(<CollectionsScreen/>);expect(await screen.findByText('Invalid collection filters.')).toBeVisible();expect(mock).not.toHaveBeenCalled();});
it('anonymous mine requires login without querying collections',async()=>{route.query='scope=mine';mount(<CollectionsScreen/>,{...sessionValue(),account:null});expect(await screen.findByRole('link',{name:'Log in to view your collections'})).toHaveAttribute('href','/login?returnTo=%2Fcollections');expect(mock).not.toHaveBeenCalled();});
it('recovers from rate limit without treating it as empty',async()=>{mock.mockResolvedValueOnce(Response.json({}, {status:429}));mount(<CollectionsScreen/>);expect(await screen.findByText('Collections unavailable.')).toBeVisible();fireEvent.click(screen.getByRole('button',{name:'Retry'}));expect(await screen.findByRole('link',{name:'Cinema'})).toBeVisible();});
it('has first-page recovery for out-of-range and a genuine empty state',async()=>{route.query='scope=mine&page=8';mock.mockImplementation(async()=>Response.json({items:[],page:8,pageSize:20,totalCount:0}));const s=sessionValue();const view=mount(<CollectionsScreen/>,s);expect(await screen.findByRole('link',{name:'Go to first page'})).toHaveAttribute('href','/collections?scope=mine');route.query='scope=mine';view.change(s,<CollectionsScreen/>);expect(await screen.findByText('No collections yet.')).toBeVisible();});
it('401 rechecks session and offers login recovery without cached list rows',async()=>{mock.mockImplementation(async()=>Response.json({}, {status:401}));const s=sessionValue();mount(<CollectionsScreen/>,s);expect(await screen.findByRole('link',{name:'Log in to view your collections'})).toBeVisible();expect(s.refresh).toHaveBeenCalledTimes(2);expect(screen.queryByRole('link',{name:'Cinema'})).toBeNull();});
