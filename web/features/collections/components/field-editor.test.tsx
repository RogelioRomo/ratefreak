// @vitest-environment jsdom
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { FieldEditor } from "./field-editor";
import { useContentsActions } from "../hooks/use-contents-actions";
import { mount, details } from "./test-support";
import { resetCsrfState } from "@/shared/api/csrf";
vi.mock('next/navigation',()=>({usePathname:()=>'/collections/test'}));
const mock=vi.fn<typeof fetch>();
beforeEach(()=>{resetCsrfState();mock.mockReset();vi.stubGlobal('fetch',mock);mock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):Response.json({id:'field',key:'notes',label:'Notes',fieldType:'text',required:false,position:0,configuration:{maxLength:1000}}));});
function Test(){const actions=useContentsActions('owner',details.collection.id,true);return <FieldEditor details={details} itemCount={1} active actions={actions} refresh={async()=>{}} reload={async()=>details} onCancel={()=>{}} onSaved={()=>{}}/>;}
it('uses native field controls and full revision-bound create with Required disabled for populated collection',async()=>{
 mount(<Test/>);fireEvent.change(screen.getByLabelText('Key'),{target:{value:'notes'}});fireEvent.change(screen.getByLabelText('Label'),{target:{value:'Notes'}});expect(screen.getByLabelText('Required')).toBeDisabled();fireEvent.click(screen.getByRole('button',{name:'Add field'}));await waitFor(()=>expect(mock.mock.calls.some(([,o])=>o?.method==='POST')).toBe(true));expect(JSON.parse(String(mock.mock.calls.find(([,o])=>o?.method==='POST')?.[1]?.body))).toEqual({expectedRevision:1,field:{key:'notes',label:'Notes',fieldType:'text',required:false,configuration:{maxLength:1000},position:0}});
});
it('preserves draft and unmapped validation errors rather than dropping backend fields[index] errors',async()=>{
 mock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):Response.json({errors:{'fields[4].label':['Backend field label'],'unexpected':['Global validation error']}},{status:400}));mount(<Test/>);fireEvent.change(screen.getByLabelText('Key'),{target:{value:'notes'}});fireEvent.change(screen.getByLabelText('Label'),{target:{value:'Notes'}});fireEvent.click(screen.getByRole('button',{name:'Add field'}));expect(await screen.findByText('Global validation error')).toBeVisible();expect(screen.getByText('Backend field label')).toBeVisible();expect(screen.getByLabelText('Label')).toHaveValue('Notes');
});
it('attaches identifiable backend field errors to labeled native controls',async()=>{
 mock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):Response.json({errors:{'fields[0].label':['Label rejected'],'unknown':['Keep this summary']}},{status:400}));mount(<Test/>);fireEvent.change(screen.getByLabelText('Key'),{target:{value:'notes'}});fireEvent.change(screen.getByLabelText('Label'),{target:{value:'Notes'}});fireEvent.click(screen.getByRole('button',{name:'Add field'}));await screen.findByText('Label rejected');expect(screen.getByLabelText('Label')).toHaveAttribute('aria-invalid','true');expect(screen.getByLabelText('Label')).toHaveAccessibleDescription('Label rejected');expect(screen.getByText('Keep this summary')).toBeVisible();
});
