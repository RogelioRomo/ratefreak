// @vitest-environment jsdom
import {render,screen} from '@testing-library/react';
import {QueryClient,QueryClientProvider} from '@tanstack/react-query';
import {beforeEach,expect,it,vi} from 'vitest';
import {ProfileScreen} from './profile-screen';
const state=vi.hoisted(()=>({data:{id:'owner',handle:'owner',displayName:'Visible name',bio:'Visible bio',createdAt:'2026-11-09',profileTextHidden:false},account:{id:'reporter',handle:'reporter'},isLoading:false,isTransitioning:false,error:null}));
vi.mock('../hooks/use-profile',()=>({useProfile:()=>({data:state.data,isPending:false,isError:false})}));
vi.mock('@/features/auth/hooks/use-session',()=>({useSession:()=>state}));
vi.mock('@/features/follows/components/connections',()=>({Connections:()=> <p>Follow controls remain available</p>}));
vi.mock('next/navigation',()=>({usePathname:()=>'/users/owner',useSearchParams:()=>new URLSearchParams()}));
beforeEach(()=>{state.data={id:'owner',handle:'owner',displayName:'Visible name',bio:'Visible bio',createdAt:'2026-11-09',profileTextHidden:false};state.account={id:'reporter',handle:'reporter'};});
function setup(){render(<QueryClientProvider client={new QueryClient()}><ProfileScreen handle="owner"/></QueryClientProvider>);}
it('offers profile reporting to another account',()=>{setup();expect(screen.getByRole('button',{name:'Report profile'})).toBeVisible();});
it('does not offer self-reporting',()=>{state.account={id:'owner',handle:'owner'};setup();expect(screen.queryByRole('button',{name:'Report profile'})).toBeNull();});
it('explains hidden profile text and keeps the profile usable without another report control',()=>{
 state.data={...state.data,displayName:'owner',bio:'',profileTextHidden:true};setup();
 expect(screen.getByText('Profile text is currently unavailable.')).toBeVisible();expect(screen.queryByRole('button',{name:'Report profile'})).toBeNull();expect(screen.getByText('Follow controls remain available')).toBeVisible();
});
