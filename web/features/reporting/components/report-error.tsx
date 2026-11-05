import { ApiError } from '@/shared/api/api-error';
import type { ReportErrorProps } from '../interfaces/reporting';
export function ReportError({error}: ReportErrorProps) {
 if (!error) return null;
 const status = error instanceof ApiError ? error.status : 0;
 const seconds = error instanceof ApiError && error.retryAfter && /^\d+$/.test(error.retryAfter) && Number(error.retryAfter) > 0 && Number(error.retryAfter) <= 86400 ? Number(error.retryAfter) : null;
 const message = status === 400 ? 'Please check the report reason and details.' : status === 401 ? 'Please check your session and log in again before reporting.' : status === 403 ? 'Your session cannot submit this report. Check your session before trying again.' : status === 429 ? `Too many reports. ${seconds ? `Wait ${seconds} seconds` : 'Please wait'} before trying again.` : 'The report could not be submitted.';
 return <p role="alert">{message}</p>;
}
