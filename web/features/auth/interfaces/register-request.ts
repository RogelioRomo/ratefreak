import type { LoginRequest } from "./login-request";

export interface RegisterRequest extends LoginRequest {
  handle: string;
  displayName: string;
}
