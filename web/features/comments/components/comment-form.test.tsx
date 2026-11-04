// @vitest-environment jsdom
import { fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider, QueryObserver } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import { CommentForm } from "./comment-form";
import { ApiError } from "@/shared/api/api-error";
const mocks=vi.hoisted(()=>({create:vi.fn(),update:vi.fn()}));
vi.mock('../api/comments',()=>({createComment:mocks.create,updateComment:mocks.update}));
vi.mock('@/features/auth/hooks/use-session',()=>({useSession:()=>({account:{id:'owner'},isLoading:false,isTransitioning:false,error:null})}));
function setup(){const client=new QueryClient();return {client,...render(<QueryClientProvider client={client}><CommentForm accountId="owner" available reviewId="review" /></QueryClientProvider>)};}
it('preserves draft, associates validation errors and permits correction',async()=>{mocks.create.mockRejectedValue(new ApiError(400,{errors:{body:['Invalid text']}}));setup();fireEvent.change(screen.getByLabelText('Comment'),{target:{value:'Draft'}});fireEvent.click(screen.getByRole('button',{name:'Post comment'}));await screen.findByText('Invalid text');expect(screen.getByLabelText('Comment')).toHaveValue('Draft');expect(screen.getByLabelText('Comment')).toHaveAttribute('aria-invalid','true');expect(screen.getByRole('button',{name:'Post comment'})).toBeEnabled();});
it('blocks another POST after unknown outcome',async()=>{mocks.create.mockRejectedValue(new TypeError('offline'));setup();fireEvent.change(screen.getByLabelText('Comment'),{target:{value:'Draft'}});fireEvent.click(screen.getByRole('button',{name:'Post comment'}));await screen.findByText(/couldn’t confirm/i);expect(screen.getByRole('button',{name:'Post comment'})).toBeDisabled();expect(screen.getByLabelText('Comment')).toHaveValue('Draft');});
it('confirms write even when refreshing comments fails',async()=>{mocks.create.mockResolvedValue({id:'saved'});const view=setup();const observer=new QueryObserver(view.client,{queryKey:['comments','review',1],queryFn:async()=>{throw new Error('read offline');},retry:false});const stop=observer.subscribe(()=>{});fireEvent.change(screen.getByLabelText('Comment'),{target:{value:'Draft'}});fireEvent.click(screen.getByRole('button',{name:'Post comment'}));await screen.findByText(/Comment posted/);expect(screen.getByLabelText('Comment')).toHaveValue('');expect(mocks.create).toHaveBeenCalledWith('review',{body:'Draft',isSpoiler:false},expect.any(AbortSignal));expect(observer.getCurrentResult().isError).toBe(true);stop();});
