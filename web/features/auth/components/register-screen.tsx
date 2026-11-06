import { AuthPanel } from "./auth-panel";
import { RegisterForm } from "./register-form";

export function RegisterScreen() {
  return <AuthPanel title="Make yourself at home." description="Create your account. Bring your own taste."><RegisterForm /></AuthPanel>;
}
