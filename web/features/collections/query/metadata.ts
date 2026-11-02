import type { CollectionCreate, CollectionMetadata } from "../interfaces/collections";
import { ApiError } from "@/shared/api/api-error";
export function metadataErrors(value: CollectionMetadata): Record<string,string[]> {
  const errors: Record<string,string[]> = {};
  if (!value.name.trim() || value.name.trim().length > 120) errors.name = ["Name must contain 1–120 characters."];
  if (value.description.length > 2000) errors.description = ["Description must contain at most 2000 characters."];
  if (!["private", "public"].includes(value.visibility)) errors.visibility = ["Choose Private or Public."];
  return errors;
}
export function metadataInput(value: CollectionMetadata): CollectionCreate { return {name:value.name.trim(),description:value.description || null,visibility:value.visibility}; }
export function fieldErrors(error: Error | null): Record<string,string[]> { return error instanceof ApiError ? error.problem?.errors ?? {} : {}; }
export function writeMessage(error: Error | null): string | null {
  if (!error) return null;
  if (error instanceof ApiError && error.status === 429) return "Requests are limited. Wait before trying again; your draft is preserved.";
  if (error instanceof ApiError && error.status === 401) return "Your session expired. Log in to continue.";
  return "We couldn’t save your collection. Your draft is preserved; check the fields and try again.";
}
