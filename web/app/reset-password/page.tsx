import type { Metadata } from "next";
import { ResetPasswordScreen } from "@/features/auth/components/reset-password-screen";
export const metadata: Metadata = { title: "Reset password · RateFreak", robots: { index: false, follow: false } };
export default function ResetPasswordPage() { return <ResetPasswordScreen />; }
