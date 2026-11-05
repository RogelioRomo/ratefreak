import Link from 'next/link';
import type {StaffPagingProps} from '../interfaces/moderation';
import styles from './moderation.module.css';
export function StaffPaging({page,total,href}:StaffPagingProps){return <nav className={styles.paging} aria-label="Moderation pages">{page>1&&<Link href={href(page-1)}>Previous</Link>}<span>Page {page}</span>{page*20<total&&page<10000&&<Link href={href(page+1)}>Next</Link>}{page>1&&page*20>=total+20&&<Link href={href(1)}>First page</Link>}</nav>;}
