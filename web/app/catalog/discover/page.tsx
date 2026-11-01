import { Suspense } from "react";
import { DiscoveryScreen } from "@/features/providers/components/discovery-screen";
import { LoadingState } from "@/components/ui/loading-state";
export const metadata = { title: "Find titles | RateFreak" };
export default function DiscoveryPage() {
  return <Suspense fallback={<LoadingState label="Loading discovery…" />}><DiscoveryScreen /></Suspense>;
}
