import {ApiError} from '@/shared/api/api-error';
import {Button} from '@/components/ui/button';
export function StaffFeedback({error,onRetry}:{error:Error|null;onRetry:()=>void}){
 if(!error)return null;const status=error instanceof ApiError?error.status:0;
 return <div role="alert"><p>{status===404?'This staff resource is no longer available.':status===429?'Moderation requests are limited. Wait before reloading.':'The staff inspection could not be loaded. Content is concealed until a fresh inspection succeeds.'}</p><Button variant="secondary" onClick={onRetry}>Reload inspection</Button></div>;
}
