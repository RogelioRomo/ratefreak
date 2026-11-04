import { useQuery } from "@tanstack/react-query";
import { getComments, getDiscussion } from "../api/comments";
export const commentKeys = { all: ["comments"] as const, review: (id: string) => ["comments", id.toLowerCase()] as const };
export const useDiscussion = (id: string, enabled: boolean) => useQuery({ queryKey: ["review-discussion", id.toLowerCase()], queryFn: ({ signal }) => getDiscussion(id, signal), enabled, retry: false });
export const useComments = (id: string, page: number) => useQuery({ queryKey: [...commentKeys.review(id), page], queryFn: ({ signal }) => getComments(id, page, signal), retry: false });
