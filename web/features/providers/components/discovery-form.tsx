"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { IconSearch } from "@tabler/icons-react";
import { Button } from "@/components/ui/button";
import { mediaTypes } from "@/features/catalog/query/filters";
import type { DiscoveryFormProps } from "../interfaces/component-props";
import type { ProviderName } from "../interfaces/provider";
import { providerNames, validProviderType } from "../query/identity";
import { discoveryHref } from "../query/filters";
import styles from "./providers.module.css";
export function DiscoveryForm({ capabilities, filters }: DiscoveryFormProps) {
  const [provider, setProvider] = useState(filters.provider);
  const [type, setType] = useState(filters.type);
  const router = useRouter();
  const cap = capabilities.find(x => x.name === provider)!;
  const types = cap.supportedTypes.filter(t => validProviderType(provider, t));
  return <form className={styles.form} role="search" aria-label="Provider search" onSubmit={event => {
    event.preventDefault(); const values = new FormData(event.currentTarget);
    router.push(discoveryHref({ q: String(values.get("q") ?? "").trim(), provider, type, page: 1 }));
  }}>
    <div className={styles.search}><label htmlFor="discovery-title">Title</label><input id="discovery-title" name="q" type="search" required maxLength={200} defaultValue={filters.q} placeholder="Search beyond the local catalog" /></div>
    <div><label htmlFor="discovery-provider">Provider</label><select id="discovery-provider" value={provider} onChange={event => {
      const next = event.target.value as ProviderName; setProvider(next);
      const nextTypes = capabilities.find(x => x.name === next)!.supportedTypes.filter(t => validProviderType(next, t));
      if (!nextTypes.includes(type)) setType(nextTypes[0]);
    }}>{Object.keys(providerNames).map(name => { const option = capabilities.find(x => x.name === name); return <option key={name} value={name} disabled={!option?.enabled}>{providerNames[name as ProviderName]}{!option?.enabled ? " (unavailable)" : ""}</option>; })}</select></div>
    <div><label htmlFor="discovery-type">Media type</label><select id="discovery-type" value={type} onChange={event => setType(event.target.value as typeof type)}>{types.map(t => <option key={t} value={t}>{mediaTypes[t]}</option>)}</select></div>
    <Button type="submit"><IconSearch size={18} aria-hidden="true" />Search providers</Button>
  </form>;
}
