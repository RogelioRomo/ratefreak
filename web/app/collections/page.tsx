import { Suspense } from "react";
import { CollectionsScreen } from "@/features/collections/components/collections-screen";
import { LoadingState } from "@/components/ui/loading-state";
export const metadata = {title: "Collections | RateFreak"};
export default function CollectionsPage() { return <Suspense fallback={<LoadingState label="Loading collections…"/>}><CollectionsScreen/></Suspense>; }
