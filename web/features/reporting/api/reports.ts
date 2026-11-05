import { apiRequest } from '@/shared/api/request';
import type { ReportAcknowledgement, ReportInput, ReportTarget } from '../interfaces/reporting';
export function createReport(target: ReportTarget, input: ReportInput, signal?: AbortSignal): Promise<ReportAcknowledgement> {
 return apiRequest<ReportAcknowledgement>(`${target.type === 'review' ? 'reviews' : 'comments'}/${target.id}/reports`, { method: 'POST', body: input, signal });
}
