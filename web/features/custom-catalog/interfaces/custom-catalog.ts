import type { MediaDetails } from "@/features/catalog/interfaces/media-details";
export interface Category { id: string; name: string; scope: "public" | "private"; revision: number; isHidden?: boolean; isSeeded?: boolean; creatorUserId?: string | null; }
export interface CategoryPage { items: Category[]; page: number; pageSize: number; totalCount: number; }
export interface CustomItem extends MediaDetails { category: Category; creatorUserId: string; visibility: "private" | "public"; revision: number; isArchived: boolean; isHidden: boolean; }
export interface ItemInput { title: string; description: string | null; categoryId: string; visibility: "private" | "public"; }
export interface CategoryPickerProps { viewer: string; value: string; onChange: (id: string) => void; selected?: Category; disabled?: boolean; }
export interface ItemEditorProps { initial?: CustomItem; viewer: string; active: boolean; }
export interface CatalogCategoryFilterProps { defaultValue?: string; onChange?: (id: string) => void; }
export interface CatalogReportControlsProps { item: MediaDetails; }
export interface CategoryManagementProps { viewer: string; active: boolean; }
export interface CategoryManagementEditorProps extends CategoryManagementProps { category: Category; admin?: boolean; onSaved: () => void; targets?: Category[]; }
export interface ManagedItemIdProps { id: string; }
export interface ManagedItemProps extends ManagedItemIdProps { viewer: string; active: boolean; }
export interface OwnItemsProps { viewer: string; active: boolean; }
