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
it('shows the associated album and links to its validated MusicBrainz identity',()=>{
 render(<MusicMetadata metadata={{release:{id:'df86f3ac-6da2-3e5c-b4ba-5f8abb29ce51',title:'Year Zero',releaseGroupId:'3c0ed061-55d0-3cf2-bdb8-d88425e0f3ea'}}} compact />);
 expect(screen.getByRole('link',{name:'Year Zero'})).toHaveAttribute('href','https://musicbrainz.org/release-group/3c0ed061-55d0-3cf2-bdb8-d88425e0f3ea');
});
it('links release-only associations and ignores unsafe IDs and malformed titles',()=>{
 const view=render(<MusicMetadata metadata={{release:{id:'df86f3ac-6da2-3e5c-b4ba-5f8abb29ce51',title:'Year Zero'}}} />);
 expect(screen.getByRole('link',{name:'Year Zero'})).toHaveAttribute('href','https://musicbrainz.org/release/df86f3ac-6da2-3e5c-b4ba-5f8abb29ce51');
 view.rerender(<MusicMetadata metadata={{release:{id:'https://evil.test',releaseGroupId:'javascript:alert(1)',title:'Untrusted'}}} />);
 expect(screen.queryByRole('link')).not.toBeInTheDocument();
});
