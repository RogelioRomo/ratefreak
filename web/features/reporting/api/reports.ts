import { apiRequest } from '@/shared/api/request';
import type { ReportAcknowledgement, ReportInput, ReportTarget } from '../interfaces/reporting';
export function createReport(target: ReportTarget, input: ReportInput, signal?: AbortSignal): Promise<ReportAcknowledgement> {
 return apiRequest<ReportAcknowledgement>(`${{ review: 'reviews', comment: 'comments', item: 'media', category: 'categories' }[target.type]}/${target.id}/reports`, { method: 'POST', body: input, signal });
}
