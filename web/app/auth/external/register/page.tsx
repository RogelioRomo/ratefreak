import type { Metadata } from "next";
import { ExternalRegistrationScreen } from "@/features/auth/components/external-registration-screen";
export const metadata: Metadata = { title: "Finish creating account · RateFreak", robots: { index: false, follow: false } };
export default function ExternalRegistrationPage() { return <ExternalRegistrationScreen />; }
