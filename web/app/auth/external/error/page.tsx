import type { Metadata } from "next";
import Link from "next/link";
import { AuthPanel } from "@/features/auth/components/auth-panel";
import styles from "@/features/auth/components/auth.module.css";
export const metadata: Metadata = { title: "Sign-in interrupted · RateFreak", robots: { index: false, follow: false } };
export default function ExternalErrorPage() {
  return <AuthPanel title="Let’s try that again." description="Your provider sign-in couldn’t be completed.">
    <div className={styles.success}><p>The request may have expired or been cancelled. Start a new sign-in to continue.</p>
    <p>If you already have an account with that email, log in to it first, then link Google or GitHub from account settings.</p>
    <p><Link href="/login">Back to login</Link></p><p><Link href="/account">Go to account settings</Link></p></div>
  </AuthPanel>;
}
