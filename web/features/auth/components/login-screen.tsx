import { AuthPanel } from "./auth-panel";
import { AuthEntry } from "./auth-entry";
import { LoginForm } from "./login-form";

export function LoginScreen() {
  return <AuthPanel title="Welcome back." description="Your favorites are right where you left them."><AuthEntry><LoginForm /></AuthEntry></AuthPanel>;
}
