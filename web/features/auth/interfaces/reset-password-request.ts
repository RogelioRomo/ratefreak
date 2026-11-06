export interface ResetPasswordRequest {
  handle: string;
  recoveryCode: string;
  password: string;
}
