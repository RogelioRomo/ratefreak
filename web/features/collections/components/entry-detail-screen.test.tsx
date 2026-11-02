// @vitest-environment jsdom
import { screen } from '@testing-library/react';import { expect,it,vi } from 'vitest';
import { EntryDetailScreen } from './entry-detail-screen';import { mount,details,id,sessionValue } from './test-support';
vi.mock('next/navigation',()=>({usePathname:()=>'/collections/title/items/item',useSearchParams:()=>new URLSearchParams(),useRouter:()=>({push:vi.fn()})}));
it('displays literal values and hides management controls from anonymous viewers',async()=>{
 const field={id:'field',key:'text',label:'Notes',fieldType:'text',required:false,position:0,configuration:{}};vi.stubGlobal('fetch',vi.fn(async(url)=>String(url).includes('/items/')?Response.json({id,mediaItemId:null,title:'Custom entry',data:{text:'<b>Literal</b>\nSecond line'},position:0,createdAt:'d',updatedAt:'d'}):Response.json({...details,fields:[field]})));
 mount(<EntryDetailScreen collectionId={id} itemId={id}/>,{...sessionValue(),account:null});expect(await screen.findByRole('heading',{name:'Custom entry'})).toBeVisible();expect(screen.getByText(/<b>Literal<\/b>/)).toBeVisible();expect(screen.queryByRole('button',{name:'Edit entry'})).toBeNull();
});
