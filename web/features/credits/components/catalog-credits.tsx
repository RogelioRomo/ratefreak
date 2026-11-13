import Link from "next/link";
import type { ProviderName } from "@/features/providers/interfaces/provider";
import { providerHomes, providerNames } from "@/features/providers/query/identity";
import type { CatalogCreditsProps } from "../interfaces/catalog-credits-props";
import styles from "./credits.module.css";

export function CatalogCredits({ type, providers }: CatalogCreditsProps) {
  const known = [...new Set(providers)].filter((provider): provider is ProviderName => Object.hasOwn(providerNames, provider));
  if (type === "custom" || known.length === 0) return null;
  return <aside className={styles.catalog} aria-label="Metadata sources">
    <p>Metadata provided by {known.map((provider, index) => <span key={provider}>{index > 0 && ", "}<a href={providerHomes[provider]}>{providerNames[provider]}</a></span>)}.</p>
    <Link href="/credits">Provider credits</Link>
  </aside>;
}
