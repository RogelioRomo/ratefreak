import { hashKey,type QueryClient,type QueryKey } from '@tanstack/react-query';
export function clearObsoleteContents(client:QueryClient,viewer:string,id:string,keep:QueryKey):void{
 const active=hashKey(keep),normalized=id.toLowerCase();
 client.removeQueries({predicate:query=>query.queryKey[0]==='collections'&&query.queryHash!==active&&(query.queryKey[3]===normalized||(query.queryKey[1]!==viewer&&query.queryKey[2]==='list'))});
 void client.invalidateQueries({predicate:query=>query.queryKey[0]==='collections'&&query.queryKey[2]==='list'});
}
