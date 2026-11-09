import { ManageItemScreen } from "@/features/custom-catalog/components/manage-item-screen";
export default async function Page({ params }: { params: Promise<{ id: string }> }) { const { id } = await params; return <ManageItemScreen id={id} />; }
