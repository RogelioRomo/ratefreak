import { Suspense } from "react";
import { MediaDetailScreen } from "@/features/catalog/components/media-detail-screen";
import { LoadingState } from "@/components/ui/loading-state";
import type { MediaPageProps } from "@/features/catalog/interfaces/component-props";
export const metadata = { title: "Media details | RateFreak" };
async function MediaContent({ params }: MediaPageProps) {
  const { id } = await params;
  return <MediaDetailScreen id={id} />;
}
export default function MediaPage(props: MediaPageProps) {
  return <Suspense fallback={<LoadingState label="Loading title…" />}><MediaContent {...props} /></Suspense>;
}
