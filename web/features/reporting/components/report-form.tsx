'use client';
import { useEffect, useId, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { ApiError } from '@/shared/api/api-error';
import { fieldErrors } from '@/features/auth/components/form-error';
import { createReport } from '../api/reports';
import { useReportWrite } from '../hooks/use-report-write';
import type { ReportFormProps, ReportReason } from '../interfaces/reporting';
import { ReportError } from './report-error';
import styles from './reporting.module.css';
export function ReportForm({target,available,onCancel}: ReportFormProps) {
 const id = useId(), write = useReportWrite(available);
 const [reason,setReason] = useState<ReportReason>('spam'), [details,setDetails] = useState('');
 const [error,setError] = useState<Error|null>(null), [uncertain,setUncertain] = useState(false), [message,setMessage] = useState('');
 const [validation,setValidation] = useState('');
 const reasons = fieldErrors(error,'reason'), detailErrors = fieldErrors(error,'details');
 useEffect(() => { document.getElementById(`${id}-reason`)?.focus(); }, [id]);
 if (message) return <p role="status" hidden={!available}>{message}</p>;
 return <form aria-label={`Report ${target.type}`} className={styles.form} hidden={!available} inert={!available} onSubmit={async event => {
  event.preventDefault(); if (!available || write.pending || uncertain) return;
  const text = details.trim();
  if ((reason === 'other' && !text) || text.length > 1000) { setValidation('Enter 1–1000 characters for Other; other reasons allow up to 1000.'); document.getElementById(`${id}-details`)?.focus(); return; }
  setValidation(''); setError(null);
  const result = await write.run(signal => createReport(target,{reason,details:text || null},signal));
  if (result.kind === 'saved') { setDetails(''); setMessage('Report submitted.'); }
  else if (result.kind === 'rejected') {
   const status = result.error instanceof ApiError ? result.error.status : 0;
   if (status === 409) { setDetails(''); setMessage('You have already reported this content.'); }
   else if (status === 404) setMessage('This content is no longer available to report.');
   else setError(result.error);
  } else setUncertain(true);
 }}>
  <p>Report this {target.type}. Your report details are sent to moderators.</p>
  <ReportError error={error}/>
  {uncertain && <p role="alert">We couldn’t confirm submission. Reload before trying again. Reloading discards this draft. <Button variant="secondary" onClick={() => window.location.reload()}>Reload page</Button></p>}
  <fieldset disabled={!available || write.pending || uncertain}>
   <label htmlFor={`${id}-reason`}>Reason</label>
   <Select id={`${id}-reason`} value={reason} onChange={event => setReason(event.target.value as ReportReason)} aria-invalid={reasons?.length ? true : undefined} aria-describedby={reasons?.length ? `${id}-reason-error` : undefined}>
    <option value="spam">Spam</option><option value="harassment">Harassment</option><option value="hate">Hate</option><option value="sexual_content">Sexual content</option><option value="violence">Violence</option><option value="spoiler">Spoiler</option><option value="other">Other</option>
   </Select>{reasons?.length && <p id={`${id}-reason-error`} role="alert">{reasons.join(' ')}</p>}
   <label htmlFor={`${id}-details`}>Details</label>
   <textarea id={`${id}-details`} rows={4} maxLength={1000} required={reason === 'other'} value={details} onChange={event => setDetails(event.target.value)} aria-invalid={!!validation || !!detailErrors?.length || undefined} aria-describedby={`${id}-hint${validation || detailErrors?.length ? ` ${id}-details-error` : ''}`} />
   <p id={`${id}-hint`}>{reason === 'other' ? 'Required for Other. ' : 'Optional. '}Up to 1000 characters.</p>
   {(validation || detailErrors?.length) && <p id={`${id}-details-error`} role="alert">{validation || detailErrors?.join(' ')}</p>}
   <div className={styles.actions}><Button type="submit">{write.pending ? 'Submitting…' : 'Submit report'}</Button>{!uncertain && <Button variant="secondary" onClick={onCancel}>Cancel report</Button>}</div>
  </fieldset>
 </form>;
}
