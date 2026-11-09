import Link from 'next/link';
import styles from './moderation.module.css';
export function StaffNav(){return <nav aria-label="Moderation" className={styles.nav}><Link href="/moderation">Reports</Link><Link href="/moderation/audit">Audit history</Link><Link href="/catalog/categories">Categories</Link><Link href="/account">Account</Link></nav>;}
