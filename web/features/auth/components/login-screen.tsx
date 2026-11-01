import type { LoginContinuationProps } from "../interfaces/login-continuation";
import { safePreviewReturnTo } from "../query/return-to";
import { AuthPanel } from "./auth-panel";
import { AuthEntry } from "./auth-entry";
import { LoginForm } from "./login-form";

export function LoginScreen({ returnTo }: LoginContinuationProps = {}) {
  const continuation = safePreviewReturnTo(returnTo ?? null);
  return <AuthPanel title="Welcome back." description="Log in to your corner of RateFreak."><AuthEntry returnTo={continuation}><LoginForm returnTo={continuation} /></AuthEntry></AuthPanel>;
}
