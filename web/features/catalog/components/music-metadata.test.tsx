// @vitest-environment jsdom
import { render, screen } from '@testing-library/react';
import { expect, it } from 'vitest';
import { MusicMetadata } from './music-metadata';
it('makes song versions identifiable with artist, duration and disambiguation',()=>{
 render(<MusicMetadata metadata={{artistCredit:'Nine Inch Nails',lengthMilliseconds:217000,disambiguation:'Live in London',firstReleaseDate:'2007-04'}} />);
 expect(screen.getByText('Nine Inch Nails')).toBeVisible();expect(screen.getByText('Live in London')).toBeVisible();expect(screen.getByText('3:37')).toBeVisible();expect(screen.getByText('First released 2007-04')).toBeVisible();
});
it('renders literal metadata and ignores invalid optional fields',()=>{
 render(<MusicMetadata metadata={{artistCredit:'<script>Artist</script>',lengthMilliseconds:-1,disambiguation:{},firstReleaseDate:'bad'}} />);
 expect(screen.getByText('<script>Artist</script>')).toBeVisible();expect(document.querySelector('script')).toBeNull();expect(screen.queryByText(/First released/)).not.toBeInTheDocument();
});
