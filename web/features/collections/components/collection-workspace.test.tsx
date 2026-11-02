// @vitest-environment jsdom
import { fireEvent,screen,waitFor } from '@testing-library/react';
import { beforeEach,expect,it,vi } from 'vitest';
import { CollectionDetailScreen } from './collection-detail-screen';
import { details,id,mount,sessionValue } from './test-support';
const route=vi.hoisted(()=>({query:'',push:vi.fn()}));vi.mock('next/navigation',()=>({usePathname:()=>`/collections/${id}`,useSearchParams:()=>new URLSearchParams(route.query),useRouter:()=>({push:route.push})}));
const mock=vi.fn<typeof fetch>();
beforeEach(()=>{route.query='';mock.mockReset();mock.mockImplementation(async url=>String(url).includes('/items?')?Response.json({items:[{id:'c1111111-1111-1111-1111-111111111111',mediaItemId:null,title:'Real custom entry',data:{},position:0,createdAt:'d',updatedAt:'d'}],page:1,pageSize:20,totalCount:21}):Response.json(details));vi.stubGlobal('fetch',mock);});
it('shows entries and fields and allows only one owner editor while preserving paging drafts',async()=>{
 const s=sessionValue();const view=mount(<CollectionDetailScreen id={id}/>,s);expect(await screen.findByRole('link',{name:'Real custom entry'})).toHaveAttribute('href',`/collections/${id}/items/c1111111-1111-1111-1111-111111111111`);fireEvent.click(screen.getByRole('button',{name:'Add entry'}));fireEvent.change(await screen.findByLabelText('Entry title'),{target:{value:'Unsaved draft'}});expect(screen.getByRole('button',{name:'Add field'})).toBeDisabled();route.query='itemsPage=2';view.change(s,<CollectionDetailScreen id={id}/>);await waitFor(()=>expect(screen.getByLabelText('Entry title')).toHaveValue('Unsaved draft'));
});
it('invalid query rejects entries reads and anonymous readers have no owner controls',async()=>{
 route.query='itemsPage=0';const s=sessionValue();const view=mount(<CollectionDetailScreen id={id}/>,s);expect(await screen.findByText('Invalid entry page.')).toBeVisible();expect(mock).not.toHaveBeenCalled();route.query='';view.change({...s,account:null},<CollectionDetailScreen id={id}/>);await screen.findByRole('link',{name:'Real custom entry'});expect(screen.queryByRole('button',{name:'Add entry'})).toBeNull();
});
