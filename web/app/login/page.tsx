import type { Metadata } from "next";
import { LoginScreen } from "@/features/auth/components/login-screen";

export const metadata: Metadata = { title: "Log in · RateFreak" };
export default function LoginPage() { return <LoginScreen />; }
