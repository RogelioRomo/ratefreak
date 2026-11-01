import type { Metadata } from "next";
import type { ChildrenProps } from "@/shared/interfaces/children-props";
import { Providers } from "./providers";
import { AppShell } from "@/components/layout/app-shell";
import { SessionProvider } from "@/features/auth/components/session-provider";
import { THEME_BOOTSTRAP } from "@/shared/theme/theme-bootstrap";
import "./globals.css";

export const metadata: Metadata = {
  title: "RateFreak",
  description: "Your home for movies, TV, music, games, and the collections you create.",
};

export default function RootLayout({ children }: ChildrenProps) {
  return (
    <html lang="en" data-theme="dark" suppressHydrationWarning>
      <head><script dangerouslySetInnerHTML={{ __html: THEME_BOOTSTRAP }} /></head>
      <body><Providers><SessionProvider><AppShell>{children}</AppShell></SessionProvider></Providers></body>
    </html>
  );
}
