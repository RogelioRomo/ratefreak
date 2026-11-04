import { apiRequest } from "@/shared/api/request";
import type { AccountProfile } from "@/features/auth/interfaces/account-profile";
import type { PublicProfile, ProfileInput } from "../interfaces/profile";
export const getProfile = (handle: string, signal?: AbortSignal) => apiRequest<PublicProfile>(`users/by-handle/${handle}`, { signal });
export const saveProfile = (body: ProfileInput, signal?: AbortSignal) => apiRequest<AccountProfile>("users/me", { method: "PATCH", body, signal });
