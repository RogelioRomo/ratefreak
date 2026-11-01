import { Suspense } from "react";
import { ProviderPreviewScreen } from "@/features/providers/components/provider-preview-screen";
import { LoadingState } from "@/components/ui/loading-state";
import type { ProviderPreviewPageProps } from "@/features/providers/interfaces/component-props";
export const metadata = { title: "Provider preview | RateFreak" };
async function PreviewContent({ params }: ProviderPreviewPageProps) {
  return <ProviderPreviewScreen {...await params} />;
}
export default function PreviewPage(props: ProviderPreviewPageProps) {
  return <Suspense fallback={<LoadingState label="Loading preview…" />}><PreviewContent {...props} /></Suspense>;
}
