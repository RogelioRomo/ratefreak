// @vitest-environment jsdom
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { mount } from "../components/test-support";
import { useContentsActions } from "./use-contents-actions";
vi.mock('next/navigation',()=>({usePathname:()=>'/collections/test'}));
it('keeps a confirmed success separate from failed refresh and retries reads alone',async()=>{
 const mutation=vi.fn(async()=>({id:'saved'}));let reads=0;const refresh=async()=>{if(++reads===1)throw new Error('offline');};
 function Test(){const action=useContentsActions('owner','id',true);return <><button onClick={()=>void action.execute(mutation,refresh)}>Save</button><button onClick={()=>void action.retryRefresh()}>Refresh</button><p>{action.needsRefresh?'Saved; refresh needed':'Ready'}</p></>;}
 mount(<Test/>);fireEvent.click(screen.getByText('Save'));expect(await screen.findByText('Saved; refresh needed')).toBeVisible();fireEvent.click(screen.getByText('Save'));expect(mutation).toHaveBeenCalledTimes(1);fireEvent.click(screen.getByText('Refresh'));await waitFor(()=>expect(reads).toBe(2));expect(await screen.findByText('Ready')).toBeVisible();expect(mutation).toHaveBeenCalledTimes(1);
});
it('closes a confirmed editor across interrupted refresh and same-account resume before unlocking writes',async()=>{
 const {useState}=await import('react');const mutation=vi.fn(async()=>({id:'saved'}));let reads=0,started=false;
 const refresh=(signal:AbortSignal)=>{reads++;if(reads===1){started=true;return new Promise<void>((_resolve,reject)=>signal.addEventListener('abort',()=>reject(new DOMException('Cancelled','AbortError')),{once:true}));}return Promise.resolve();};
 function Test({active=true}){const action=useContentsActions('owner','id',active);const [closed,setClosed]=useState(false);return <><button disabled={closed} onClick={()=>void action.execute(mutation,refresh).then(result=>{if(result)setClosed(true);})}>Save editor</button><button onClick={()=>void action.retryRefresh()}>Refresh saved state</button><p>{action.needsRefresh?'Refresh needed':'Ready'}</p></>;}
 const {sessionValue}=await import('../components/test-support');const s=sessionValue();const view=mount(<Test/>,s);fireEvent.click(screen.getByText('Save editor'));await waitFor(()=>expect(started).toBe(true));view.change(s,<Test active={false}/>);view.change(s,<Test/>);await waitFor(()=>expect(screen.getByText('Refresh needed')).toBeVisible());fireEvent.click(screen.getByText('Refresh saved state'));await waitFor(()=>expect(screen.getByText('Ready')).toBeVisible());expect(screen.getByText('Save editor')).toBeDisabled();expect(mutation).toHaveBeenCalledTimes(1);
});
