import { Suspense } from "react";
import { CatalogScreen } from "@/features/catalog/components/catalog-screen";
import { LoadingState } from "@/components/ui/loading-state";
export const metadata = { title: "Catalog | RateFreak" };
export default function CatalogPage() {
  return <Suspense fallback={<LoadingState label="Loading catalog…" />}><CatalogScreen /></Suspense>;
}
