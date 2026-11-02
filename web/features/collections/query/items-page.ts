export function itemsPage(params:URLSearchParams):number|null{
 for(const key of params.keys())if(key!=='itemsPage'||params.getAll(key).length!==1)return null;
 const value=params.get('itemsPage')??'1';return /^\d+$/.test(value)&&+value>=1&&+value<=10000?+value:null;
}
export function itemsHref(id:string,page:number):string{return `/collections/${id.toLowerCase()}${page>1?`?itemsPage=${page}`:''}`;}
