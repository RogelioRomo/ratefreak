import { QueryClient } from '@tanstack/react-query';import { expect,it } from 'vitest';
import { clearObsoleteContents } from './refresh-caches';
it('removes old page/item/viewer variants while retaining the active locator and unrelated collections',()=>{
 const client=new QueryClient(),id='id',keep=['collections','owner','page-snapshot',id,1];
 client.setQueryData(keep,'active');client.setQueryData(['collections','owner','page-snapshot',id,2],'stale page');client.setQueryData(['collections','anonymous','entry-snapshot',id,'entry'],'old public entry');client.setQueryData(['collections','owner','item',id,1,1,'entry'],'old revision');client.setQueryData(['collections','owner','page-snapshot','unrelated',1],'other');
 clearObsoleteContents(client,'owner',id,keep);expect(client.getQueryData(keep)).toBe('active');expect(client.getQueryData(['collections','owner','page-snapshot',id,2])).toBeUndefined();expect(client.getQueryData(['collections','anonymous','entry-snapshot',id,'entry'])).toBeUndefined();expect(client.getQueryData(['collections','owner','item',id,1,1,'entry'])).toBeUndefined();expect(client.getQueryData(['collections','owner','page-snapshot','unrelated',1])).toBe('other');client.clear();
});
