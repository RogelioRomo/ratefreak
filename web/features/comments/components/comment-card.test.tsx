// @vitest-environment jsdom
import { fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import { CommentCard } from "./comment-card";
const session=vi.hoisted(()=>({account:{id:'owner'},error:null,isLoading:false,isTransitioning:false}));
vi.mock('@/features/auth/hooks/use-session',()=>({useSession:()=>session}));
const remove=vi.hoisted(()=>vi.fn());vi.mock('../api/comments',()=>({deleteComment:remove}));
const comment={id:'comment',reviewId:'review',author:{id:'owner',handle:'writer',displayName:'Writer'},body:'Secret spoiler',isSpoiler:true,createdAt:'2026-11-04',updatedAt:'2026-11-04'};
function setup(owner='owner'){const client=new QueryClient();client.setQueryData(['session'],{id:owner});return render(<QueryClientProvider client={client}><CommentCard comment={comment}/></QueryClientProvider>);}
it('conceals spoiler body until explicitly revealed',()=>{setup();expect(screen.queryByText('Secret spoiler')).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Show comment spoilers'}));expect(screen.getByText('Secret spoiler')).toBeVisible();});
it('does not give another author editing controls',()=>{setup('review-owner');expect(screen.queryByRole('button',{name:'Edit comment'})).toBeNull();expect(screen.queryByRole('button',{name:'Delete comment'})).toBeNull();});
it('requires deletion confirmation and cancel makes no request',async()=>{setup();fireEvent.click(screen.getByRole('button',{name:'Delete comment'}));expect(screen.queryByText('Secret spoiler')).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Keep comment'}));expect(remove).not.toHaveBeenCalled();fireEvent.click(screen.getByRole('button',{name:'Delete comment'}));remove.mockResolvedValue(undefined);fireEvent.click(screen.getByRole('button',{name:'Confirm delete'}));await screen.findByText('Comment deleted.');});
