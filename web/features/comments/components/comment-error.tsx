import { ApiError } from "@/shared/api/api-error";
import type { CommentErrorProps } from "../interfaces/comments";
export function CommentError({ error }: CommentErrorProps) {
 if (!error) return null;
 const status = error instanceof ApiError ? error.status : 0;
 const message = status === 400 ? "Please check your comment." : status === 429 ? "Too many changes. Wait before trying again." : status === 401 ? "Please check your session and log in again." : status === 404 ? "This review or comment is no longer available to change." : "The comment could not be saved.";
 return <p role="alert">{message}</p>;
}
