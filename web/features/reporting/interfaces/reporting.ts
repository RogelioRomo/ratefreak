export type ReportReason = 'spam' | 'harassment' | 'hate' | 'sexual_content' | 'violence' | 'spoiler' | 'other';
export interface ReportTarget { type: 'review' | 'comment' | 'item' | 'category' | 'profile'; id: string; authorId: string }
export interface ReportInput { reason: ReportReason; details?: string | null }
export interface ReportAcknowledgement { id: string; status: string; createdAt: string }
export interface ReportControlProps { target: ReportTarget; available?: boolean }
export interface ReportFormProps { target: ReportTarget; accountId: string; available: boolean; onCancel: () => void }
export type ReportWriteResult = { kind: 'saved' } | { kind: 'rejected'; error: Error } | { kind: 'uncertain' };
export interface ReportErrorProps { error: Error | null }
