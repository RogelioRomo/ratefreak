import type { QueryClient } from "@tanstack/react-query";
import type { CommentDetails, CommentPage } from "../interfaces/comments";
import { commentKeys } from "../hooks/use-discussion";
export function updateCachedComment(client: QueryClient, comment: CommentDetails): void {
 client.setQueriesData<CommentPage>({ queryKey: commentKeys.review(comment.reviewId) }, page => page ? { ...page, items: page.items.map(item => item.id === comment.id ? comment : item) } : page);
}
export function removeCachedComment(client: QueryClient, reviewId: string, id: string): void {
 client.setQueriesData<CommentPage>({ queryKey: commentKeys.review(reviewId) }, page => page ? { ...page, totalCount: Math.max(0, page.totalCount - 1), items: page.items.filter(item => item.id !== id) } : page);
}
