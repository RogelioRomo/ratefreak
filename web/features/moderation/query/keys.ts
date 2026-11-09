import type {QueryClient} from '@tanstack/react-query';
export const staffKeys={account:(id:string)=>['staff',id] as const};
export function refreshModeration(client:QueryClient,accountId:string){
 void client.invalidateQueries({queryKey:staffKeys.account(accountId)});
 for(const root of ['community','review-discussion','comments','personal','catalog','categories','custom-items','collections'])void client.invalidateQueries({queryKey:[root]});
}
