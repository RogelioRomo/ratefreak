import { apiRequest } from "@/shared/api/request";
import type { RecoveryCodeResponse } from "../interfaces/recovery-code-response";
import type { RecoveryStatus } from "../interfaces/recovery-status";
import type { ResetPasswordRequest } from "../interfaces/reset-password-request";
import type { GenerateRecoveryCodeRequest } from "../interfaces/generate-recovery-code-request";

export function getRecoveryStatus(signal?: AbortSignal): Promise<RecoveryStatus> {
  return apiRequest("auth/recovery-status", { signal });
}
export function generateRecoveryCode(input: GenerateRecoveryCodeRequest): Promise<RecoveryCodeResponse> {
  return apiRequest("auth/recovery-code", { method: "POST", body: input });
}
export function resetPassword(input: ResetPasswordRequest): Promise<RecoveryCodeResponse> {
  return apiRequest("auth/reset-password", { method: "POST", body: input });
}
