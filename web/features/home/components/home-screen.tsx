'use client';
import { useSession } from '@/features/auth/hooks/use-session';
import { FeedScreen } from '@/features/feeds/components/feed-screen';
import { LoadingState } from '@/components/ui/loading-state';
import { Button } from '@/components/ui/button';
import { HomeIntro } from './home-intro';
export function HomeScreen() {
  const session=useSession();
  if(session.error)return <section style={{paddingBlock:'3rem'}}><h1>Home unavailable</h1><p role="alert">Your session could not be checked.</p><Button onClick={()=>void session.refresh()}>Retry session</Button></section>;
  if(session.account===undefined || session.isTransitioning)return <LoadingState label="Checking your session…"/>;
  return session.account ? <FeedScreen key={session.account.id} accountId={session.account.id}/> : <HomeIntro/>;
}
