// @vitest-environment jsdom
import { fireEvent,screen,waitFor } from '@testing-library/react';import { expect,it,vi } from 'vitest';
import { EntryRemoval } from './entry-removal';import { mount,details,id } from './test-support';import { useContentsActions } from '../hooks/use-contents-actions';import { resetCsrfState } from '@/shared/api/csrf';
vi.mock('next/navigation',()=>({usePathname:()=>'/collections/title/items/item'}));
it('loads a fresh snapshot before confirmation and removes only on explicit consent',async()=>{
 resetCsrfState();const mock=vi.fn<typeof fetch>(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):new Response(null,{status:204}));vi.stubGlobal('fetch',mock);const snapshot={details,item:{id,mediaItemId:null,title:'Entry to remove',data:{},position:0,createdAt:'d',updatedAt:'d'}};const reload=vi.fn(async()=>snapshot);
 function Test(){const actions=useContentsActions('owner',id,true);return <EntryRemoval snapshot={snapshot} reload={reload} active actions={actions} refresh={async()=>{}} onCancel={()=>{}} onSaved={()=>{}}/>;}
 mount(<Test/>);fireEvent.click(screen.getByRole('button',{name:'Review entry removal'}));expect(await screen.findByText(/Entry to remove/)).toBeVisible();expect(mock).not.toHaveBeenCalled();fireEvent.click(screen.getByRole('button',{name:'Confirm remove entry'}));await waitFor(()=>expect(mock.mock.calls.some(([,o])=>o?.method==='DELETE')).toBe(true));expect(mock.mock.calls.find(([,o])=>o?.method==='DELETE')?.[0]).toBe(`/api/v1/collections/${id}/items/${id}?expectedRevision=1`);
});
