import type { Metadata } from "next";
import { AccountScreen } from "@/features/auth/components/account-screen";

export const metadata: Metadata = { title: "Your account · RateFreak" };
export default function AccountPage() { return <AccountScreen />; }
