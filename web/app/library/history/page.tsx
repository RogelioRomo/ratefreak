import { Suspense } from "react";
import { HistoryScreen } from "@/features/personal/components/history-screen";
import { LoadingState } from "@/components/ui/loading-state";
export const metadata = { title: "Your history | RateFreak" };
export default function HistoryPage() { return <Suspense fallback={<LoadingState label="Loading your history…" />}><HistoryScreen /></Suspense>; }
