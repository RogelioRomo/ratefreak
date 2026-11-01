"use client";
import Link from "next/link";
import { ApiError } from "@/shared/api/api-error";
import { ErrorState } from "@/components/ui/error-state";
import type { PersonalErrorProps, PersonalReadErrorProps } from "../interfaces/component-props";
export function PersonalError({ error }: PersonalErrorProps) {
  if (!error) return null;
  const status = error instanceof ApiError ? error.status : 0;
  const fields = error instanceof ApiError ? Object.values(error.problem?.errors ?? {}).flat() : [];
  return <div role="alert"><p>{status === 401 ? "Your session expired. Please log in again." : status === 429 ? "Too many requests. Please wait before trying again." : status === 409 ? "A review already exists. Your draft has been kept." : status === 404 ? "This record is no longer available. Reload before trying again." : status === 400 ? "Please check the fields below." : "We couldn’t save this change. Please try again."}</p>{fields.map((message, index) => <p key={index}>{message}</p>)}{status === 401 && <Link href="/login">Log in</Link>}</div>;
}
export function PersonalReadError({ error, retry }: PersonalReadErrorProps) {
  return <ErrorState title={error instanceof ApiError && error.status === 401 ? "Your session expired." : "Saved data unavailable."} description={error instanceof ApiError && error.status === 429 ? "Please wait before trying again." : "Your saved fields could not be loaded. Try again before editing."} onRetry={retry} />;
}
