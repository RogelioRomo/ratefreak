import { Suspense } from 'react';
import { HomeScreen } from '@/features/home/components/home-screen';
import { LoadingState } from '@/components/ui/loading-state';
export default function Home() {
  return <Suspense fallback={<LoadingState label="Loading home…"/>}><HomeScreen/></Suspense>;
}
