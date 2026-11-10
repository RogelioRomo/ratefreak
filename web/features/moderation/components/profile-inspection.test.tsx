// @vitest-environment jsdom
import {render,screen} from '@testing-library/react';
import {expect,it} from 'vitest';
import {TargetContent} from './target-content';
it('labels the current profile fields and explains restore behavior without rendering markup',()=>{
 render(<TargetContent target={{id:'owner',targetType:'profile',author:{id:'owner',handle:'owner',displayName:'owner'},body:'<b>Hidden name</b>',description:'<script>bio</script>',isSpoiler:false,hidden:true,updatedAt:'2026-11-09T12:00:00Z'}}/>);
 expect(screen.getByText('Display name')).toBeVisible();expect(screen.getByText('Bio')).toBeVisible();expect(screen.getByText('<b>Hidden name</b>')).toBeVisible();expect(document.querySelector('b')).toBeNull();
 expect(screen.getByText(/Restoring publishes the current saved name and bio/)).toBeVisible();
});
