import { AuthPanel } from "./auth-panel";
import { AuthEntry } from "./auth-entry";
import { RegisterForm } from "./register-form";

export function RegisterScreen() {
  return <AuthPanel title="Make yourself at home." description="Create your account. Bring your own taste."><AuthEntry><RegisterForm /></AuthEntry></AuthPanel>;
}
