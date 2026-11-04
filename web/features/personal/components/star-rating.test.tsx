// @vitest-environment jsdom
import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { expect,it,vi } from 'vitest';
import { StarRating } from './star-rating';
function Control(){const [value,setValue]=useState<number|null>(null);return <StarRating value={value} onChange={setValue}/>;}
it('selects every half-star increment and fills five icons',()=>{render(<Control/>);expect(screen.getByRole('group',{name:'Your score'})).toBeVisible();for(let score=.5;score<=5;score+=.5){fireEvent.click(screen.getByRole('radio',{name:`${score} ${score===1?'star':'stars'}`}));expect(screen.getByRole('radio',{name:`${score} ${score===1?'star':'stars'}`})).toBeChecked();expect(screen.getByText(`${score.toFixed(1)} / 5`)).toBeVisible();}expect(document.querySelectorAll('[data-rating-star]')).toHaveLength(5);});
it('previews half stars without committing and restores the selected value',()=>{const onChange=vi.fn();render(<StarRating value={3.5} onChange={onChange}/>);const half=screen.getByRole('radio',{name:'4.5 stars'}).closest('label')!;fireEvent.pointerEnter(half,{pointerType:'mouse'});expect(screen.getByText('4.5 / 5')).toBeVisible();expect(onChange).not.toHaveBeenCalled();fireEvent.pointerLeave(screen.getByRole('group',{name:'Your score'}));expect(screen.getByText('3.5 / 5')).toBeVisible();});
it('supports saved zero and disables all choices during writes',()=>{render(<StarRating value={0} disabled onChange={vi.fn()}/>);expect(screen.getByRole('radio',{name:'0 stars'})).toBeChecked();for(const radio of screen.getAllByRole('radio'))expect(radio).toBeDisabled();expect(screen.getByText('0.0 / 5')).toBeVisible();});
