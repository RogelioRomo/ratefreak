import type { AccountProfile } from "./account-profile";
import type { RecoveryCodeResponse } from "./recovery-code-response";
export interface RegistrationResponse extends AccountProfile, RecoveryCodeResponse {}
