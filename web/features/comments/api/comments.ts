import { apiRequest } from "@/shared/api/request";
import type { ReviewDetails } from "@/features/personal/interfaces/personal";
import type { CommentDetails, CommentInput, CommentPage } from "../interfaces/comments";
export const getDiscussion = (id: string, signal?: AbortSignal) => apiRequest<ReviewDetails>(`reviews/${id}`, { signal });
export const getComments = (id: string, page: number, signal?: AbortSignal) => apiRequest<CommentPage>(`reviews/${id}/comments?page=${page}&pageSize=20`, { signal });
export const createComment = (id: string, body: CommentInput, signal?: AbortSignal) => apiRequest<CommentDetails>(`reviews/${id}/comments`, { method: "POST", body, signal });
export const updateComment = (id: string, body: CommentInput, signal?: AbortSignal) => apiRequest<CommentDetails>(`comments/${id}`, { method: "PATCH", body, signal });
export const deleteComment = (id: string, signal?: AbortSignal) => apiRequest<void>(`comments/${id}`, { method: "DELETE", body: {}, signal });
