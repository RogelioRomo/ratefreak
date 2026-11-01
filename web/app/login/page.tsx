import type { Metadata } from "next";
import { Suspense } from "react";
import { LoginScreen } from "@/features/auth/components/login-screen";
import { LoadingState } from "@/components/ui/loading-state";
import type { LoginPageProps } from "@/features/auth/interfaces/login-continuation";
export const metadata: Metadata = { title: "Log in · RateFreak" };
async function LoginContent({ searchParams }: LoginPageProps) {
  const params = await searchParams;
  return <LoginScreen returnTo={typeof params.returnTo === "string" ? params.returnTo : null} />;
}
export default function LoginPage(props: LoginPageProps) {
  return <Suspense fallback={<LoadingState label="Loading login…" />}><LoginContent {...props} /></Suspense>;
}
