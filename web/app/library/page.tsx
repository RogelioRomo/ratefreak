import { Suspense } from "react";
import { LibraryScreen } from "@/features/personal/components/library-screen";
import { LoadingState } from "@/components/ui/loading-state";
export const metadata = { title: "Your library | RateFreak" };
export default function LibraryPage() {
  return <Suspense fallback={<LoadingState label="Loading your library…" />}><LibraryScreen /></Suspense>;
}
