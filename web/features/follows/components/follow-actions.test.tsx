// @vitest-environment jsdom
import { fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import { FollowActions } from "./follow-actions";
const state=vi.hoisted(()=>({account:{id:'viewer'},isTransitioning:false,isLoading:false,error:null,refresh:vi.fn()}));
vi.mock('@/features/auth/hooks/use-session',()=>({useSession:()=>state}));
const set=vi.hoisted(()=>vi.fn());vi.mock('../api/follows',()=>({setFollow:set}));
function setup(id='target') { const client=new QueryClient(); return render(<QueryClientProvider client={client}><FollowActions userId={id} handle="target" available /></QueryClientProvider>); }
it('does not offer self follow',()=>{setup('viewer');expect(screen.queryByRole('button')).toBeNull();});
it('offers explicit repeat-safe setters and reports known save separately from reads',async()=>{set.mockResolvedValue(undefined);setup();fireEvent.click(screen.getByRole('button',{name:'Follow'}));await screen.findByText('Follow saved.');expect(set).toHaveBeenCalledWith('target',true,expect.any(AbortSignal));expect(screen.getByRole('button',{name:'Unfollow'})).toBeEnabled();});
it('explains uncertain outcomes and requires a deliberate repeat',async()=>{set.mockRejectedValue(new TypeError('offline'));setup();fireEvent.click(screen.getByRole('button',{name:'Follow'}));await screen.findByText(/couldn’t confirm/i);expect(screen.getByRole('button',{name:'Follow'})).toBeEnabled();});
