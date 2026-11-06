import { ApiError } from "@/shared/api/api-error";
import { apiRequest } from "@/shared/api/request";
import type { RegistrationResponse } from "@/features/auth/interfaces/registration-response";
import type { InstallationStatus } from "../interfaces/installation-status";
import type { SetupRequest } from "../interfaces/setup-request";

export async function getInstallationStatus(signal?: AbortSignal): Promise<InstallationStatus> {
  const status = await apiRequest<InstallationStatus>("setup/status", { signal });
  if (!status || typeof status.required !== "boolean" || typeof status.configured !== "boolean") {
    throw new ApiError(502, { title: "Invalid installation status." });
  }
  return status;
}
export function completeInstallation(input: SetupRequest): Promise<RegistrationResponse> {
  return apiRequest("setup", { method: "POST", body: input });
}
