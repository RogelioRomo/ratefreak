import { Suspense } from "react";
import { LoadingState } from "@/components/ui/loading-state";
import { CollectionDetailScreen } from "@/features/collections/components/collection-detail-screen";
import type { CollectionPageProps } from "@/features/collections/interfaces/component-props";
export const metadata = {title: "Collection | RateFreak"};
async function Content({params}: CollectionPageProps) {const {id} = await params; return <CollectionDetailScreen id={id}/>;}
export default function CollectionPage(props: CollectionPageProps) {return <Suspense fallback={<LoadingState label="Loading collection…"/>}><Content {...props}/></Suspense>;}
