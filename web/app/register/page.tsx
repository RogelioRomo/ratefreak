import type { Metadata } from "next";
import { RegisterScreen } from "@/features/auth/components/register-screen";

export const metadata: Metadata = { title: "Create account · RateFreak" };
export default function RegisterPage() { return <RegisterScreen />; }
