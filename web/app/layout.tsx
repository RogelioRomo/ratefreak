import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "RateFreak",
  description: "Your home for movies, TV, music, and the collections you create.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
