"use client";
import { CatalogCategoryFilter } from "@/features/custom-catalog/components/catalog-category-filter";
import { useCatalogViewer } from "@/features/custom-catalog/hooks/use-catalog-viewer";
import { Select } from "@/components/ui/select";
import Link from 'next/link';
import { useId,useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getCatalog } from '@/features/catalog/api/catalog';
import { mediaTypes } from '@/features/catalog/query/filters';
import type { CatalogFilters } from '@/features/catalog/interfaces/catalog-filters';
import { Button } from '@/components/ui/button';
import type { CatalogPickerProps } from '../interfaces/contents-props';
import { ContentsError } from './contents-error';
import styles from './collections.module.css';
export function CatalogPicker({value,onChange,disabled}:CatalogPickerProps){
 const { viewer, active } = useCatalogViewer();
 const id=useId();const [categoryId,setCategoryId]=useState('');const [q,setQ]=useState(''),[type,setType]=useState<CatalogFilters['type']>(''),[filters,setFilters]=useState<CatalogFilters|null>(null);
 const query=useQuery({queryKey:['catalog','collection-picker',viewer,filters],queryFn:({signal})=>getCatalog(filters!,signal),enabled:active&&!!filters&&!disabled,retry:false});
 const search=()=>setFilters({q:q.trim(),type,page:1,pageSize:20,...(categoryId?{categoryId}:{})});
 return <section className={styles.picker} aria-label="Choose a catalog title"><div className={styles.field}><label htmlFor={`${id}-q`}>Catalog title search</label><input id={`${id}-q`} value={q} maxLength={200} disabled={disabled} onChange={e=>setQ(e.target.value)} onKeyDown={e=>{if(e.key==='Enter'){e.preventDefault();search();}}}/></div><div className={styles.field}><label htmlFor={`${id}-type`}>Catalog item type</label><Select id={`${id}-type`} value={type} disabled={disabled} onChange={e=>setType(e.target.value as CatalogFilters['type'])}><option value="">All types</option>{Object.entries(mediaTypes).map(([key,label])=><option key={key} value={key}>{label}</option>)}</Select></div><CatalogCategoryFilter onChange={setCategoryId}/><Button type="button" variant="secondary" disabled={disabled} onClick={search}>Search catalog</Button>
 {value&&<p role="status">Selected: {value.title} <Button type="button" variant="secondary" disabled={disabled} onClick={()=>onChange(null)}>Clear selection</Button></p>}
 {filters&&(query.isPending?<p role="status">Searching catalog…</p>:query.isError?<><ContentsError error={query.error}/><Button type="button" variant="secondary" disabled={disabled} onClick={()=>void query.refetch()}>Retry catalog search</Button></>:<><ul className={styles.fieldList}>{query.data.items.map(item=><li key={item.id}><span>{item.title}</span><Button type="button" variant="secondary" disabled={disabled} onClick={()=>onChange({id:item.id,title:item.title})} aria-label={`Choose ${item.title}`}>Choose title</Button></li>)}</ul>{!query.data.items.length&&<p>No matching catalog titles.</p>}<div className={styles.actions}><Button type="button" variant="secondary" disabled={disabled||filters.page<=1} onClick={()=>setFilters({...filters,page:filters.page-1})}>Previous catalog page</Button><span>Page {filters.page}</span><Button type="button" variant="secondary" disabled={disabled||filters.page>=10000||filters.page*20>=query.data.totalCount} onClick={()=>setFilters({...filters,page:filters.page+1})}>Next catalog page</Button></div></>)}
 <p className={styles.hint}>Missing an item? <Link href="/catalog/new">Create an item first</Link> or <Link href="/catalog/discover" target="_blank" rel="noopener noreferrer">Find titles from providers in a new tab</Link>, then search the local catalog again.</p>
 </section>;
}
