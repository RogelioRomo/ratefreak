import { decimalText,isDecimalValue } from '../query/exact-decimal';
import type { EntryValuesProps } from '../interfaces/contents-props';
import styles from './collections.module.css';
export function EntryValues({fields,data}:EntryValuesProps){return fields.length?<dl className={styles.valueList}>{[...fields].sort((a,b)=>a.position-b.position||a.id.localeCompare(b.id)).map(field=>{const value=data[field.key];return <div key={field.id}><dt>{field.label}</dt><dd>{value===undefined||value===null?'No value':isDecimalValue(value)?decimalText(value):typeof value==='boolean'?value?'Yes':'No':value===''?'Empty text':value}</dd></div>;})}</dl>:<p className={styles.hint}>This collection has no custom fields.</p>;}
