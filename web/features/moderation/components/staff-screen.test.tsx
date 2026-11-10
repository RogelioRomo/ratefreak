// @vitest-environment jsdom
import {fireEvent,render,screen,waitFor,within} from '@testing-library/react';
import {QueryClient,QueryClientProvider} from '@tanstack/react-query';
import {beforeEach,expect,it,vi} from 'vitest';
import {StaffContext} from '../query/context';
import {accessStore} from '../query/access';
import {ApiError} from '@/shared/api/api-error';
import {StaffScreen} from './staff-screen';
const api=vi.hoisted(()=>({getStaffAccounts:vi.fn(),setModerator:vi.fn(),revokeAdmin:vi.fn()}));
vi.mock('../api/staff-management',()=>api);
vi.mock('next/navigation',()=>({useSearchParams:()=>new URLSearchParams('staffOnly=false')}));
const admin={id:'admin',handle:'owner',displayName:'Owner',isAdmin:true,isModerator:false};
const account={id:'reader',handle:'reader',displayName:'Reader',isAdmin:false,isModerator:false};
beforeEach(()=>{api.getStaffAccounts.mockReset().mockResolvedValue({items:[admin,account],totalCount:2,page:1,pageSize:20});api.setModerator.mockReset().mockResolvedValue(undefined);api.revokeAdmin.mockReset().mockResolvedValue(undefined);});
function setup(){const client=new QueryClient({defaultOptions:{queries:{retry:false}}});const ui=(active:boolean,id='staff')=><QueryClientProvider client={client}><StaffContext.Provider value={{accountId:id,active}}><StaffScreen/></StaffContext.Provider></QueryClientProvider>;return {client,ui,...render(ui(true))};}
async function choose(){fireEvent.click(await screen.findByRole('button',{name:'Grant moderator'}));return screen.findByLabelText('Appointment reason');}
it('lists public identities, offers Admin removal and existing audit history',async()=>{
 setup();await screen.findByText('@owner');expect(screen.getByText('Administrator · Moderation access included')).toBeVisible();
 expect(screen.getAllByRole('button',{name:'Grant moderator'})).toHaveLength(1);
 expect(screen.getByRole('button',{name:'Revoke Admin'})).toBeVisible();
 expect(screen.getByLabelText('Search accounts')).toHaveAttribute('maxLength','100');
 expect(screen.getByRole('link',{name:'Audit history for @reader'})).toHaveAttribute('href','/moderation/audit?targetType=account&targetId=reader');
});
it('confirms Admin removal separately and refreshes remaining roles',async()=>{
 setup();fireEvent.click(await screen.findByRole('button',{name:'Revoke Admin'}));
 expect(screen.getByText(/Revoke Admin access for @owner/)).toBeVisible();expect(screen.getByRole('button',{name:'Confirm Admin removal'})).toBeDisabled();
 fireEvent.change(screen.getByLabelText('Admin removal reason'),{target:{value:'  Staff departure  '}});
 api.getStaffAccounts.mockResolvedValue({items:[{...admin,isAdmin:false,isModerator:true},account],totalCount:2,page:1,pageSize:20});
 fireEvent.click(screen.getByRole('button',{name:'Confirm Admin removal'}));
 await screen.findByText('Admin access removed.');await screen.findByRole('button',{name:'Revoke moderator'});
 expect(api.revokeAdmin).toHaveBeenCalledWith('admin','Staff departure',expect.any(AbortSignal));expect(api.setModerator).not.toHaveBeenCalled();
});
it('clears a previous success message when a new role decision begins',async()=>{
 setup();fireEvent.click(await screen.findByRole('button',{name:'Revoke Admin'}));fireEvent.change(screen.getByLabelText('Admin removal reason'),{target:{value:'Remove'}});
 api.getStaffAccounts.mockResolvedValue({items:[{...admin,isAdmin:false,isModerator:true},account],totalCount:2,page:1,pageSize:20});fireEvent.click(screen.getByRole('button',{name:'Confirm Admin removal'}));await screen.findByText('Admin access removed.');
 await waitFor(()=>expect(screen.getByRole('button',{name:'Grant moderator'})).toBeEnabled());fireEvent.click(screen.getByRole('button',{name:'Grant moderator'}));expect(screen.queryByText('Admin access removed.')).toBeNull();
});
it('explains last-Admin refusal and requires a refresh before retrying',async()=>{
 api.revokeAdmin.mockRejectedValue(new ApiError(409));setup();fireEvent.click(await screen.findByRole('button',{name:'Revoke Admin'}));fireEvent.change(screen.getByLabelText('Admin removal reason'),{target:{value:'Removal'}});
 fireEvent.click(screen.getByRole('button',{name:'Confirm Admin removal'}));await screen.findByText('The last Admin cannot be removed. Appoint another Admin before trying again.');
 expect(screen.getByRole('button',{name:'Confirm Admin removal'})).toBeDisabled();expect(api.revokeAdmin).toHaveBeenCalledTimes(1);expect(screen.getByRole('button',{name:'Reload account roles'})).toBeEnabled();
});
it('cancels Admin removal without writing and restores focus',async()=>{
 setup();fireEvent.click(await screen.findByRole('button',{name:'Revoke Admin'}));fireEvent.click(screen.getByRole('button',{name:'Cancel appointment'}));
 expect(api.revokeAdmin).not.toHaveBeenCalled();expect(screen.getByRole('button',{name:'Revoke Admin'})).toHaveFocus();
});
it('conceals admin content immediately after self-removal while preserving Moderator access',async()=>{
 api.getStaffAccounts.mockResolvedValue({items:[{...admin,id:'staff',isModerator:true},account],totalCount:2,page:1,pageSize:20});const {client}=setup();
 fireEvent.click(await screen.findByRole('button',{name:'Revoke Admin'}));fireEvent.change(screen.getByLabelText('Admin removal reason'),{target:{value:'Step down'}});fireEvent.click(screen.getByRole('button',{name:'Confirm Admin removal'}));
 await screen.findByText('Administrator access required.');expect(screen.queryByText('@reader')).toBeNull();expect(accessStore(client).denied('staff')).toBe(0);expect(accessStore(client).denied('staff','admin')).toBe(403);
});
it('requires a reason, confirms the exact account, cancels without writing and restores focus',async()=>{
 setup();const reason=await choose();expect(reason).toHaveFocus();
 expect(screen.getByRole('button',{name:'Confirm grant'})).toBeDisabled();
 fireEvent.change(reason,{target:{value:'   '}});expect(screen.getByRole('button',{name:'Confirm grant'})).toBeDisabled();
 fireEvent.click(screen.getByRole('button',{name:'Cancel appointment'}));expect(api.setModerator).not.toHaveBeenCalled();expect(screen.getByRole('button',{name:'Grant moderator'})).toHaveFocus();
});
it('saves an appointment with a trimmed reason and refreshes current roles',async()=>{
 setup();const reason=await choose();fireEvent.change(reason,{target:{value:'  Trusted reviewer  '}});
 api.getStaffAccounts.mockResolvedValue({items:[admin,{...account,isModerator:true}],totalCount:2,page:1,pageSize:20});
 fireEvent.click(screen.getByRole('button',{name:'Confirm grant'}));
 await screen.findByText('Moderator appointment saved.');await screen.findByRole('button',{name:'Revoke moderator'});
 expect(api.setModerator).toHaveBeenCalledWith('reader',true,'Trusted reviewer',expect.any(AbortSignal));
});
it('requires reloading account roles after an uncertain write and never retries it',async()=>{
 api.setModerator.mockRejectedValue(new TypeError('network'));setup();const reason=await choose();fireEvent.change(reason,{target:{value:'Trust'}});fireEvent.click(screen.getByRole('button',{name:'Confirm grant'}));
 const reload=await screen.findByRole('button',{name:'Reload account roles'});expect(screen.getByRole('button',{name:'Confirm grant'})).toBeDisabled();
 expect(api.setModerator).toHaveBeenCalledTimes(1);fireEvent.click(reload);await waitFor(()=>expect(screen.queryByLabelText('Appointment reason')).toBeNull());
});
it('keeps moderator access intact after admin denial and conceals prior account data',async()=>{
 const {client}=setup();await screen.findByText('@reader');api.getStaffAccounts.mockRejectedValue(new ApiError(403));
 await client.invalidateQueries({queryKey:['staff','staff','admin']});await screen.findByText('Administrator access required.');
 expect(screen.queryByText('@reader')).toBeNull();expect(accessStore(client).denied('staff')).toBe(0);expect(screen.getByRole('link',{name:'Reports'})).toHaveAttribute('href','/moderation');
});
it('conceals staff data and aborts pending work during a session transition',async()=>{
 const {rerender,ui}=setup();await screen.findByText('@reader');const reason=await choose();fireEvent.change(reason,{target:{value:'Trust'}});
 let signal:AbortSignal|undefined;api.setModerator.mockImplementation((_id,_grant,_reason,s)=>{signal=s;return new Promise(()=>{});});
 fireEvent.click(screen.getByRole('button',{name:'Confirm grant'}));await waitFor(()=>expect(signal).toBeDefined());rerender(ui(false));
 expect(screen.queryByText('@reader')).toBeNull();expect(signal?.aborted).toBe(true);
});
it('revokes with explicit confirmation and a reason',async()=>{
 api.getStaffAccounts.mockResolvedValue({items:[{...account,isModerator:true}],totalCount:1,page:1,pageSize:20});setup();
 fireEvent.click(await screen.findByRole('button',{name:'Revoke moderator'}));fireEvent.change(screen.getByLabelText('Appointment reason'),{target:{value:'No longer on staff'}});
 expect(within(screen.getByRole('region',{name:'Moderator appointment for @reader'})).getByText(/Revoke moderator access for @reader/)).toBeVisible();
 fireEvent.click(screen.getByRole('button',{name:'Confirm revoke'}));await waitFor(()=>expect(api.setModerator).toHaveBeenCalledWith('reader',false,'No longer on staff',expect.any(AbortSignal)));
});
