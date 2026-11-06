import type { RegisterRequest } from "@/features/auth/interfaces/register-request";
export interface SetupRequest extends RegisterRequest {
  setupSecret: string;
}
