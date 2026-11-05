'use client';
import {createContext,useContext} from 'react';
import type {StaffContextValue} from '../interfaces/moderation';
export const StaffContext=createContext<StaffContextValue|null>(null);
export function useStaff(){const context=useContext(StaffContext);if(!context)throw Error('Staff boundary missing.');return context;}
