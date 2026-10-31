import type { Metadata } from "next";
import type { ChildrenProps } from "@/shared/interfaces/children-props";
import { Providers } from "./providers";
import { AppShell } from "@/components/layout/app-shell";
import "./globals.css";

export const metadata: Metadata = {
  title: "RateFreak",
  description: "Your home for movies, TV, music, games, and the collections you create.",
};

export default function RootLayout({ children }: ChildrenProps) {
  return (
    <html lang="en">
      <body><Providers><AppShell>{children}</AppShell></Providers></body>
    </html>
  );
}
