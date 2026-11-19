// @vitest-environment jsdom
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { mount } from "@/features/collections/components/test-support";
import { CategoriesScreen } from "./categories-screen";
vi.mock("next/navigation", () => ({ usePathname: () => "/catalog/categories" }));
it("renames an owned private category with the loaded revision", async () => {
 const id = "a1111111-1111-1111-1111-111111111111"; const category = { id, name: "Garden flowers", scope: "private", revision: 3 };
 const fetchMock = vi.fn<typeof fetch>().mockImplementation(async (url, init) => String(url).endsWith("csrf") ? Response.json({ requestToken: "token" }) : init?.method === "PATCH" ? Response.json({ ...category, name: "Flowers", revision: 4 }) : Response.json({ items: [category], page: 1, pageSize: 100, totalCount: 1 })); vi.stubGlobal("fetch", fetchMock);
 mount(<CategoriesScreen />); fireEvent.click(await screen.findByRole("button", { name: "Rename Garden flowers" })); fireEvent.change(screen.getByLabelText("Category name"), { target: { value: "Flowers" } }); fireEvent.click(screen.getByRole("button", { name: "Rename category" }));
 await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => init?.method === "PATCH")).toBe(true)); const write = fetchMock.mock.calls.find(([, init]) => init?.method === "PATCH")!; expect(write[0]).toBe(`/api/v1/categories/${id}`); expect(JSON.parse(String(write[1]?.body))).toEqual({ expectedRevision: 3, name: "Flowers", reason: null });
});
it('clears cached feeds when a category change changes public display',async()=>{
 const category={id:'a1111111-1111-1111-1111-111111111111',name:'Flowers',scope:'private',revision:1};
 vi.stubGlobal('fetch',vi.fn(async(url,init)=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):init?.method==='PATCH'?Response.json({...category,name:'Garden',revision:2}):Response.json({items:[category],page:1,pageSize:100,totalCount:1})));
 const view=mount(<CategoriesScreen/>);const key=['feeds','owner','community','',null];view.client.setQueryData(key,{items:[{title:'old category'}]});
 fireEvent.click(await screen.findByRole('button',{name:'Rename Flowers'}));fireEvent.change(screen.getByLabelText('Category name'),{target:{value:'Garden'}});fireEvent.click(screen.getByRole('button',{name:'Rename category'}));await waitFor(()=>expect(view.client.getQueryData(key)).toBeUndefined());
});
