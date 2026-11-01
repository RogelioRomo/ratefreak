import styles from "./culture-artwork.module.css";

export function CultureArtwork() {
  return (
    <svg className={styles.artwork} viewBox="0 0 520 600" role="img" aria-label="Film, television, music and games brought together in a geometric illustration." focusable="false">
      <circle cx="265" cy="295" r="215" className={styles.surface} />
      <g className={styles.outline} transform="rotate(8 373 165)">
        <path d="m344 61 27 26 28-26m-28 26v15" />
        <rect x="284" y="102" width="174" height="130" rx="16" className={styles.paper} />
        <rect x="299" y="117" width="125" height="99" rx="8" className={styles.surface} />
        <path d="M438 133v15m0 16v1m0 17v1M323 246h96" />
        <circle cx="360" cy="166" r="26" className={styles.accentFill} stroke="none" />
        <path d="m354 154 17 12-17 12z" className={styles.paperFill} stroke="none" />
      </g>
      <g>
        <circle cx="239" cy="318" r="143" className={styles.record} />
        {[113, 101, 89, 77].map((radius) => <circle key={radius} cx="239" cy="318" r={radius} className={styles.groove} />)}
        <circle cx="239" cy="318" r="48" className={styles.accentFill} />
        <circle cx="239" cy="318" r="8" className={styles.paperFill} />
        <path d="m153 246 21 19m130 108 21 19" className={styles.recordGlint} />
      </g>
      <g className={styles.outline} transform="rotate(-12 122 196)">
        <path d="M40 65h165v259H40z" className={styles.paper} />
        {[82, 110, 138, 166, 194, 222, 250, 278, 306].map((y) => <g key={y} className={styles.inkFill} stroke="none"><rect x="48" y={y} width="10" height="11" rx="1" /><rect x="187" y={y} width="10" height="11" rx="1" /></g>)}
        <rect x="68" y="84" width="109" height="104" className={styles.accentFill} stroke="none" />
        <rect x="68" y="201" width="109" height="104" className={styles.surface} stroke="none" />
        <circle cx="122" cy="136" r="31" className={styles.paper} />
        <circle cx="122" cy="136" r="7" className={styles.accentFill} stroke="none" />
        <path d="M122 105v12m-31 19h12m19 19v12m19-31h12" />
        <path d="m103 233 39 21-39 21z" className={styles.inkFill} stroke="none" />
      </g>
      <g className={styles.outline} transform="rotate(10 368 454)">
        <path d="M320 399h91c14 0 24 13 27 27l14 54c5 20-15 31-30 17l-23-21h-68l-23 21c-15 14-35 3-30-17l14-54c3-14 13-27 28-27Z" className={styles.paper} />
        <path d="M310 433h25m-12-12v25" strokeWidth="5" />
        <circle cx="407" cy="425" r="5" className={styles.accentFill} stroke="none" />
        <circle cx="421" cy="439" r="5" className={styles.accentFill} stroke="none" />
        <circle cx="393" cy="439" r="5" className={styles.accentFill} stroke="none" />
        <circle cx="407" cy="453" r="5" className={styles.accentFill} stroke="none" />
        <path d="M353 450h7m13 0h7" />
        <path d="M348 399v-18c0-18 28-16 28-34" />
      </g>
      <g className={styles.outline}>
        <path d="M77 402v28m-14-14h28M411 302v18m-9-9h18" className={styles.accentStroke} />
        <circle cx="138" cy="489" r="6" className={styles.accentFill} stroke="none" />
      </g>
    </svg>
  );
}
