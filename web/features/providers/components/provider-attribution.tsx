import type { ProviderAttributionProps } from "../interfaces/component-props";
import { providerHomes, providerNames } from "../query/identity";
import styles from "./providers.module.css";
export function ProviderAttribution({ capability }: ProviderAttributionProps) {
  return <p className={styles.attribution}>{capability.attribution} <a href={providerHomes[capability.name]} target="_blank" rel="noopener noreferrer" aria-label={`${providerNames[capability.name]} attribution`}>About {providerNames[capability.name]}</a></p>;
}
