import type { Metadata } from "next";
import { CreditsScreen } from "@/features/credits/components/credits-screen";
export const metadata: Metadata = { title: "Credits · RateFreak" };
export default function Page() { return <CreditsScreen />; }
