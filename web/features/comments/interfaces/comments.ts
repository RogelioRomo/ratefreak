import type { ReviewAuthor, ReviewDetails } from "@/features/personal/interfaces/personal";
export interface CommentInput { body: string; isSpoiler: boolean }
export interface CommentDetails extends CommentInput { id: string; reviewId: string; author: ReviewAuthor; createdAt: string; updatedAt: string }
export interface CommentPage { items: CommentDetails[]; page: number; pageSize: number; totalCount: number }
export interface DiscussionProps { reviewId: string }
export interface DiscussionPageProps { params: Promise<{ id: string }> }
export interface CommentListProps extends DiscussionProps { page: number; enabled?: boolean }
export interface CommentCardProps { comment: CommentDetails; enabled?: boolean; onDeleted?: () => void }
export interface CommentFormProps extends DiscussionProps { accountId: string; available: boolean; comment?: CommentDetails; onSaved?: () => void; onCancel?: () => void }
export interface ReviewContextProps { review: ReviewDetails; available?: boolean }
export type CommentWriteResult<T> = { kind: "saved"; value: T } | { kind: "rejected"; error: Error } | { kind: "uncertain" };
export interface CommentErrorProps { error: Error | null }
export interface OwnerCommentProps extends CommentCardProps { accountId: string; available: boolean }
