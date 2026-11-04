// @vitest-environment jsdom
import { render, screen } from '@testing-library/react';
import { expect,it } from 'vitest';
import { CommunityRating } from './community-rating';
it.each([[4.7,4.5],[4.8,5],[4.25,4.5],[0,0],[5,5]])('renders average %s as %s stars, retaining exact numbers',(average,rounded)=>{render(<CommunityRating aggregate={{average,count:10}}/>);expect(screen.getByRole('img',{name:`${rounded} out of 5 stars, rounded to the nearest half star`})).toBeVisible();expect(screen.getByText(`${average.toFixed(2)} / 5 · 10 ratings`)).toBeVisible();expect(document.querySelectorAll('[data-community-star]')).toHaveLength(5);expect(screen.queryByRole('radio')).toBeNull();});
it('distinguishes no ratings from a zero-star average',()=>{render(<CommunityRating aggregate={{average:null,count:0}}/>);expect(screen.getByText('No ratings yet.')).toBeVisible();expect(screen.queryByRole('img')).toBeNull();});
it('keeps singular rating count',()=>{render(<CommunityRating aggregate={{average:3.5,count:1}}/>);expect(screen.getByText('3.50 / 5 · 1 rating')).toBeVisible();});
