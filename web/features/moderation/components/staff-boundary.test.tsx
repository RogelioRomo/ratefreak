// @vitest-environment jsdom
import {act,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {QueryClient,QueryClientProvider} from '@tanstack/react-query';
import {beforeEach,expect,it,vi} from 'vitest';
import {StaffBoundary} from './staff-boundary';
import {useStaffQuery} from '../hooks/use-staff-query';
import {useStaff} from '../query/context';
import {sessionQueryKey} from '@/features/auth/session/query-keys';
import {accessStore} from '../query/access';
import {ApiError} from '@/shared/api/api-error';
const session=vi.hoisted(()=>({account:{id:'staff'} as {id:string}|undefined|null,isTransitioning:false,isLoading:false,error:null}));
const read=vi.hoisted(()=>vi.fn());
const authorize=vi.hoisted(()=>vi.fn());
vi.mock('@/features/auth/hooks/use-session',()=>({useSession:()=>session}));
vi.mock('../api/moderation',()=>({getReports:authorize}));
vi.mock('next/navigation',()=>({usePathname:()=>'/moderation',useSearchParams:()=>new URLSearchParams()}));
beforeEach(()=>{session.account={id:'staff'};session.isTransitioning=false;read.mockReset().mockResolvedValue('Sensitive staff content');authorize.mockReset();});
function Child(){const staff=useStaff(),query=useStaffQuery<string>(['private'],read);return <><p>{query.data}</p><input aria-label="Draft" disabled={!staff.active}/></>;}
function setup(){const client=new QueryClient();client.setQueryData(sessionQueryKey,{id:'staff'});const ui=()=> <QueryClientProvider client={client}><StaffBoundary><Child/></StaffBoundary></QueryClientProvider>;return {client,ui,...render(ui())};}
it('retains same-account drafts concealed and disabled during checks',async()=>{const view=setup();await screen.findByText('Sensitive staff content');fireEvent.change(screen.getByLabelText('Draft'),{target:{value:'Draft reason'}});session.account=undefined;view.rerender(view.ui());expect(screen.getByLabelText('Draft')).not.toBeVisible();expect(screen.getByLabelText('Draft')).toBeDisabled();session.account={id:'staff'};view.rerender(view.ui());expect(screen.getByLabelText('Draft')).toHaveValue('Draft reason');});
it('unmounts private data and latches403 until explicit successful authorization',async()=>{const view=setup();await screen.findByText('Sensitive staff content');read.mockRejectedValue(new ApiError(403));await act(async()=>{await view.client.invalidateQueries({queryKey:['staff','staff']});});await screen.findByText('Staff access required.');expect(screen.queryByText('Sensitive staff content')).toBeNull();expect(view.client.getQueryData(['staff','staff','private'])).toBeUndefined();expect(accessStore(view.client).denied('staff')).toBe(403);const calls=read.mock.calls.length;view.rerender(view.ui());expect(read).toHaveBeenCalledTimes(calls);authorize.mockResolvedValue({items:[]});read.mockResolvedValue('Authorized again');fireEvent.click(screen.getByRole('button',{name:'Check staff access again'}));await screen.findByText('Authorized again');});
it('discards old-account drafts on identity change',async()=>{const view=setup();await screen.findByText('Sensitive staff content');fireEvent.change(screen.getByLabelText('Draft'),{target:{value:'Old reason'}});session.account={id:'second'};view.client.setQueryData(sessionQueryKey,session.account);view.rerender(view.ui());await waitFor(()=>expect(screen.getByLabelText('Draft')).toHaveValue(''));});
