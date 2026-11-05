// @vitest-environment jsdom
import {fireEvent,render,screen,waitFor} from '@testing-library/react';
import {QueryClient,QueryClientProvider} from '@tanstack/react-query';
import {beforeEach,expect,it,vi} from 'vitest';
import {QueueScreen} from './queue-screen';
import {ReportScreen} from './report-screen';
import {StaffContext} from '../query/context';
import {ApiError} from '@/shared/api/api-error';
const api=vi.hoisted(()=>({getReports:vi.fn(),getReport:vi.fn(),resolveReport:vi.fn()}));vi.mock('../api/moderation',()=>api);
vi.mock('next/navigation',()=>({useSearchParams:()=>new URLSearchParams()}));
const report={id:'a1111111-1111-1111-1111-111111111111',targetType:'review',targetId:'target',reporter:null,reason:'spam',status:'open',createdAt:'2026-11-05'};
const detail={report,details:'<b>Private details</b>',target:{id:'target',targetType:'review',author:{id:'writer',handle:'writer',displayName:'Writer'},body:'Secret spoiler',isSpoiler:true,hidden:false,updatedAt:'2026-11-05T00:00:00.123456Z'},decision:null,resolutionReason:null,resolvedAt:null};
beforeEach(()=>{api.getReports.mockReset().mockResolvedValue({items:[report],page:1,pageSize:20,totalCount:1});api.getReport.mockReset().mockResolvedValue(detail);api.resolveReport.mockReset().mockResolvedValue(undefined);});
function setup(ui:React.ReactNode){const client=new QueryClient({defaultOptions:{queries:{retry:false}}});render(<QueryClientProvider client={client}><StaffContext.Provider value={{accountId:'staff',active:true}}>{ui}</StaffContext.Provider></QueryClientProvider>);return client;}
it('uses actual queue counts and handles deleted reporters',async()=>{setup(<QueueScreen/>);await screen.findByText('Deleted account');expect(screen.getByRole('link',{name:/Open report/})).toHaveAttribute('href',`/moderation/reports/${report.id}`);expect(screen.getByText('Reports: 1 · Oldest first')).toBeVisible();});
it('renders report details literally while concealing spoiler bodies',async()=>{setup(<ReportScreen id={report.id}/>);expect(await screen.findByText('<b>Private details</b>')).toBeVisible();expect(document.querySelector('b')).toBeNull();expect(screen.queryByText('Secret spoiler')).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Show content spoilers'}));expect(screen.getByText('Secret spoiler')).toBeVisible();});
it('keeps confirmed204 success outside a failed follow-up inspection',async()=>{setup(<ReportScreen id={report.id}/>);await screen.findByLabelText('Moderation reason');fireEvent.change(screen.getByLabelText('Moderation reason'),{target:{value:'Dismiss'}});fireEvent.click(screen.getByRole('button',{name:'Dismiss report'}));api.getReport.mockRejectedValue(new ApiError(503));fireEvent.click(screen.getByRole('button',{name:'Confirm dismiss'}));await screen.findByText('Moderation action saved.');await screen.findByRole('button',{name:'Reload inspection'});expect(screen.getByText('Moderation action saved.')).toBeVisible();await waitFor(()=>expect(screen.getByLabelText('Moderation reason')).toBeDisabled());});
