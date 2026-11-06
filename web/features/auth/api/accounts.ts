import type { RegistrationResponse } from "../interfaces/registration-response";
import { apiRequest } from "@/shared/api/request";
import { ApiError } from "@/shared/api/api-error";
import type { AccountProfile } from "../interfaces/account-profile";
import type { LoginRequest } from "../interfaces/login-request";
import type { RegisterRequest } from "../interfaces/register-request";

export async function getCurrentAccount(signal?: AbortSignal): Promise<AccountProfile | null> {
  try { return await apiRequest<AccountProfile>("users/me", { signal }); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return null;
    throw error;
  }
}

export function login(input: LoginRequest): Promise<AccountProfile> {
  return apiRequest("auth/login", { method: "POST", body: input });
}

export function register(input: RegisterRequest): Promise<RegistrationResponse> {
  return apiRequest("auth/register", { method: "POST", body: input });
}

export async function logout(): Promise<void> {
  try { await apiRequest<void>("auth/logout", { method: "POST" }); }
  catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) throw error;
  }
}
