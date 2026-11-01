"use client";
import { ApiError } from "@/shared/api/api-error";
import { ErrorState } from "@/components/ui/error-state";
import type { ProviderErrorProps } from "../interfaces/error-props";
export function ProviderError({ error, onRetry }: ProviderErrorProps) {
  const status = error instanceof ApiError ? error.status : 0;
  return <ErrorState title={status === 404 ? "Title not found." : status === 429 ? "Too many requests." : "Provider unavailable."}
    description={status === 404 ? "This provider could not find that title. Try another search." : status === 429 || (error instanceof ApiError && error.retryAfter) ? "Please wait a moment before trying again." : status === 504 ? "The provider took too long to respond. Please try again." : "We couldn’t reach this provider. Please try again later."} onRetry={status === 404 ? undefined : onRetry} />;
}
