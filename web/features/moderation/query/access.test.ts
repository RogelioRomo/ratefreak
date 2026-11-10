import {QueryClient} from '@tanstack/react-query';
import {expect,it} from 'vitest';
import {ApiError} from '@/shared/api/api-error';
import {accessStore,staffRequest} from './access';
it('latches denial outside cache, aborts all account requests and removes only that account staff data',async()=>{
 const client=new QueryClient();client.setQueryData(['staff','one','reports'],{details:'private'});client.setQueryData(['staff','two','reports'],{details:'other'});client.setQueryData(['catalog'],{title:'public'});
 let signal!:AbortSignal,finish!:(value:string)=>void;
 const pending=staffRequest(client,'one',s=>{signal=s;return new Promise<string>(resolve=>finish=resolve);});
 await expect(staffRequest(client,'one',async()=>{throw new ApiError(403);})).rejects.toMatchObject({status:403});
 expect(signal.aborted).toBe(true);expect(client.getQueryData(['staff','one','reports'])).toBeUndefined();expect(client.getQueryData(['staff','two','reports'])).toBeDefined();expect(client.getQueryData(['catalog'])).toBeDefined();
 client.clear();expect(accessStore(client).denied('one')).toBe(403);finish('late');await expect(pending).rejects.toBeDefined();
 await expect(staffRequest(client,'one',async()=> 'should not run')).rejects.toMatchObject({status:403});
});
it('explicit authorization clearing permits a fresh read',async()=>{const client=new QueryClient(),store=accessStore(client);store.deny('one',401);store.allow('one');expect(await staffRequest(client,'one',async()=>42)).toBe(42);});
it('admin denial conceals admin data while preserving moderation requests and access',async()=>{
 const client=new QueryClient();client.setQueryData(['staff','one','admin','accounts'],{private:'roles'});client.setQueryData(['staff','one','reports'],{queue:true});
 await expect(staffRequest(client,'one',async()=>{throw new ApiError(403);},undefined,'admin')).rejects.toMatchObject({status:403});
 expect(client.getQueryData(['staff','one','admin','accounts'])).toBeUndefined();
 expect(client.getQueryData(['staff','one','reports'])).toBeDefined();expect(accessStore(client).denied('one')).toBe(0);
 expect(accessStore(client).denied('one','admin')).toBe(403);
 expect(await staffRequest(client,'one',async()=>42)).toBe(42);
});
it('an admin request returning 401 denies all staff access',async()=>{
 const client=new QueryClient();client.setQueryData(['staff','one','reports'],{queue:true});
 await expect(staffRequest(client,'one',async()=>{throw new ApiError(401);},undefined,'admin')).rejects.toMatchObject({status:401});
 expect(accessStore(client).denied('one')).toBe(401);expect(client.getQueryData(['staff','one','reports'])).toBeUndefined();
});
