import { Suspense } from 'react';
import { LoadingState } from '@/components/ui/loading-state';
import { EntryDetailScreen } from '@/features/collections/components/entry-detail-screen';
import type { EntryPageProps } from '@/features/collections/interfaces/contents-props';
export const metadata={title:'Collection entry | RateFreak'};
async function Content({params}:EntryPageProps){const {id,itemId}=await params;return <EntryDetailScreen collectionId={id} itemId={itemId}/>;}
export default function EntryPage(props:EntryPageProps){return <Suspense fallback={<LoadingState label="Loading entry…"/>}><Content {...props}/></Suspense>;}
