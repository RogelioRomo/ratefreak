import { QueryClient } from '@tanstack/react-query';
import { expect,it } from 'vitest';
import { updateCachedComment, removeCachedComment } from './cache';
const comment={id:'comment',reviewId:'review',author:{id:'owner',handle:'owner',displayName:'Owner'},body:'old',isSpoiler:false,createdAt:'date',updatedAt:'date'};
it('reflects confirmed edits/deletions even before list refresh',()=>{const client=new QueryClient();const key=['comments','review',1];client.setQueryData(key,{items:[comment],totalCount:1,page:1,pageSize:20});updateCachedComment(client,{...comment,body:'saved'});expect(client.getQueryData(key)).toMatchObject({items:[{body:'saved'}]});removeCachedComment(client,'review','comment');expect(client.getQueryData(key)).toMatchObject({items:[],totalCount:0});});
