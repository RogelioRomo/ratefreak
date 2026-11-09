import { useCatalogViewer } from "@/features/custom-catalog/hooks/use-catalog-viewer";
import { useQuery } from "@tanstack/react-query";
import { getComments, getDiscussion } from "../api/comments";
export const commentKeys = { all: ["comments"] as const, review: (id: string) => ["comments", id.toLowerCase()] as const };
export function useDiscussion(id: string, enabled: boolean) { const { viewer, active } = useCatalogViewer(); return useQuery({ queryKey: ["review-discussion", id.toLowerCase(), viewer], queryFn: ({ signal }) => getDiscussion(id, signal), enabled: active && enabled, retry: false }); }
export function useComments(id: string, page: number) { const { viewer, active } = useCatalogViewer(); return useQuery({ queryKey: [...commentKeys.review(id), viewer, page], queryFn: ({ signal }) => getComments(id, page, signal), enabled: active, retry: false }); }
