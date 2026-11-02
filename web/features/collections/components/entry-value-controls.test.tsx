// @vitest-environment jsdom
import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import { EntryValueControls } from "./entry-value-controls";
import { entryDrafts } from "../query/entry-input";
import type { CollectionField } from "../interfaces/collections";
it('keeps unset separate from false and exposes native typed controls',()=>{
 const fields:CollectionField[]=[{id:'b',key:'seen',label:'Seen',fieldType:'boolean',required:false,position:0,configuration:{}}];
 function Test(){const [draft,setDraft]=useState(()=>entryDrafts(fields));return <EntryValueControls fields={fields} value={draft} onChange={setDraft} errors={{}}/>;}
 render(<Test/>);expect(screen.getByLabelText('Seen')).toBeDisabled();fireEvent.click(screen.getByLabelText('Set a value for Seen'));expect(screen.getByLabelText('Seen')).toBeEnabled();fireEvent.change(screen.getByLabelText('Seen'),{target:{value:'false'}});expect(screen.getByLabelText('Seen')).toHaveValue('false');
});
it('associates boolean/select backend data errors with the native control',()=>{
 const fields:CollectionField[]=[{id:'b',key:'seen',label:'Seen',fieldType:'boolean',required:false,position:0,configuration:{}}];render(<EntryValueControls fields={fields} value={{seen:{set:true,value:false}}} onChange={()=>{}} errors={{'data.seen':['Invalid saved choice']}}/>);expect(screen.getByLabelText('Seen')).toHaveAccessibleDescription('Invalid saved choice');expect(screen.getByLabelText('Seen')).toHaveAttribute('aria-invalid','true');
});
