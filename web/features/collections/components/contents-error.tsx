import { ApiError } from "@/shared/api/api-error";
import type { ContentsErrorProps } from "../interfaces/contents-props";
export function ContentsError({error,summary=[],omitValidation=false}:ContentsErrorProps){
 const api=error instanceof ApiError?error:null;const messages=!omitValidation&&api?.problem?.errors?Object.values(api.problem.errors).flat():[];
 if(!error&&!summary.length)return null;
 return <div role="alert"><p>{api?.status===429?'Requests are limited. Wait before trying again.':api?.status===413?'This entry is too large to save. Reduce its content and try again.':api?.status===401?'Your session expired. Log in to continue.':api?.status===404?'This record is missing or is no longer accessible.':error?'We couldn’t complete this action. Your draft is preserved.':null}</p>{[...summary,...messages].map((message,index)=><p key={index}>{message}</p>)}</div>;
}
